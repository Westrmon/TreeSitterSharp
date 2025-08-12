using GitHub.TreeSitter;
using System.Text;

namespace TreeSitterSharp.Parser;

// 此类主要用于编辑树节点, 编辑后就会发出事件, 更新新的节点
// 同一时刻只有一个树会被编辑, 所以不需要考虑使用多线程
// 更新时机: 接受到空格, 回车的时候立即更新, 没有的话, 1s后更新或这直接不更新
internal class ParserEdit
{
    public static void ApplyEditAndReparse(string oldText, string newText, int startByte, TSTree oldTree, TSParser parser)
    {
        var edit = new TSInputEdit
        {
            start_byte = (uint)startByte,
            old_end_byte = (uint)(startByte + Encoding.UTF8.GetByteCount(oldText)),
            new_end_byte = (uint)(startByte + Encoding.UTF8.GetByteCount(newText)),
            start_point = ByteOffsetToPoint(oldText, startByte),
            old_end_point = ByteOffsetToPoint(oldText, startByte + oldText.Length),
            new_end_point = ByteOffsetToPoint(newText, startByte + newText.Length),
        };

        oldTree.edit(edit);
        var newTree = parser.parse_string(oldTree, newText);

        // newTree now contains the updated syntax tree
    }

    private static TSPoint ByteOffsetToPoint(string text, int byteOffset)
    {
        int row = 0;
        int column = 0;
        int currentOffset = 0;

        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var lineBytes = Encoding.UTF8.GetByteCount(line + "\n");
            if (currentOffset + lineBytes > byteOffset)
            {
                // 在此行中
                var consumedBytes = byteOffset - currentOffset;
                var prefix = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(line)).Substring(0, Encoding.UTF8.GetCharCount(Encoding.UTF8.GetBytes(line), 0, consumedBytes));
                column = prefix.Length;
                break;
            }

            currentOffset += lineBytes;
            row++;
        }

        return new TSPoint { row = (uint)row, column = (uint)column };
    }
}