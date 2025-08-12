using System.Collections.Concurrent;
using TreeSitterSharp.Parser;

namespace TreeSitterSharp.Theme;

internal class StyleTokenCache : IDisposable
{
    private class CacheNode
    {
        public IQueryTree Key { get; }
        public StyleTokenProvider Value { get; }
        public (uint[] Part1, string Part2) PathInfo { get; }
        public DateTime LastAccess { get; set; }

        public CacheNode(IQueryTree key, StyleTokenProvider value, (uint[], string) path)
        {
            Key = key;
            Value = value;
            PathInfo = path;
            LastAccess = DateTime.UtcNow;
        }
    }

    private readonly ConcurrentDictionary<IQueryTree, CacheNode> _cacheMap;
    private readonly ConcurrentQueue<CacheNode> _accessTracker;
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
    private readonly Timer _evictionTimer;
    private volatile bool _isDisposed;

    public int MaxCapacity { get; internal set; }

    public StyleTokenCache()
    {
        MaxCapacity = MaxCapacity < 500 ? 500 : MaxCapacity;
        _cacheMap = new ConcurrentDictionary<IQueryTree, CacheNode>();
        _accessTracker = new ConcurrentQueue<CacheNode>();

        // 每5分钟执行一次强制清理
        _evictionTimer = new Timer(EvictExpiredItems, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public StyleTokenProvider GetOrAdd(
        IQueryTree key,
        Func<IQueryTree,
        StyleTokenProvider> factory,
        (uint[], string) pathInfo)
    {
        // 无锁快速路径
        if (_cacheMap.TryGetValue(key, out var existingNode))
        {
            UpdateAccessTimestamp(existingNode);
            return existingNode.Value;
        }

        // 创建新节点
        var newNode = new CacheNode(key, factory(key), pathInfo);
        _lock.EnterReadLock();
        // 双重检查
        if (_cacheMap.TryGetValue(key, out existingNode))
        {
            UpdateAccessTimestamp(existingNode);
            return existingNode.Value;
        }

        // 添加新条目
        _cacheMap[key] = newNode;
        _accessTracker.Enqueue(newNode);

        // 触发异步淘汰检查
        if (_cacheMap.Count >= MaxCapacity)
        {
            Task.Run(EvictLeastRecentUsed);
        }

        _lock.ExitReadLock();
        return newNode.Value;
    }

    private void UpdateAccessTimestamp(CacheNode node)
    {
        try
        {
            _lock.EnterUpgradeableReadLock();
            node.LastAccess = DateTime.UtcNow;
            _accessTracker.Enqueue(node); // 通过入队记录访问顺序
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    private void EvictLeastRecentUsed()
    {
        _lock.EnterWriteLock();
        var overflow = _cacheMap.Count - MaxCapacity;
        if (overflow <= 0) return;

        var candidates = new List<CacheNode>(overflow * 2);

        // 分析访问队列找出LRU候选
        while (_accessTracker.TryDequeue(out var node))
        {
            // 跳过已更新或已移除的节点
            if (!_cacheMap.TryGetValue(node.Key, out var current) || current != node)
                continue;

            candidates.Add(node);
        }

        // 按访问时间排序并选择淘汰目标
        var toRemove = candidates
            .OrderBy(n => n.LastAccess)
            .Take(overflow)
            .ToList();

        foreach (var node in toRemove)
        {
            if (_cacheMap.TryRemove(node.Key, out _))
                SafeDispose(node.Value);
        }

        // 重新入队剩余候选（维护队列容量）
        foreach (var node in candidates.Except(toRemove))
        {
            _accessTracker.Enqueue(node);
        }
        _lock.ExitWriteLock();
    }

    private void SafeDispose(IDisposable obj)
    {
        try { obj?.Dispose(); }
        catch (Exception ex) { /* 记录日志 */ }
    }

    private void EvictExpiredItems(object state)
    {
        // 强制清理过期项（示例：超过2小时未访问）
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var expired = _cacheMap.Where(p => p.Value.LastAccess < cutoff).ToList();

        foreach (var pair in expired)
        {
            if (_cacheMap.TryRemove(pair.Key, out var node))
            {
                SafeDispose(node.Value);
                SafeDispose(node.Key);
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _evictionTimer.Dispose();

        foreach (var pair in _cacheMap)
        {
            SafeDispose(pair.Value.Value);
            SafeDispose(pair.Key);
        }
        _cacheMap.Clear();
        _lock.Dispose();
    }
}