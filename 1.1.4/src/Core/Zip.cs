using System;
using System.IO;
using System.IO.Compression;

namespace PeterLauncher.Core
{
    /// <summary>
    /// 内建 ZIP / JAR 解包器（只用 .NET 4.0 自带的 DeflateStream，不依赖 System.IO.Compression.FileSystem）。
    /// 支持 store(0) 与 deflate(8)；不支持 zip64。
    /// </summary>
    public static class Zip
    {
        private const uint EndOfCentralDir = 0x06054b50;
        private const uint CentralDirEntry = 0x02014b50;

        /// <summary>解出压缩包全部内容到 destDir。</summary>
        public static void Extract(string zipPath, string destDir, Action<int, string> progress)
        {
            Extract(zipPath, destDir, null, progress);
        }

        /// <summary>按 filter 过滤条目名（filter 返回 false 的条目跳过）。</summary>
        public static void Extract(string zipPath, string destDir, Func<string, bool> filter, Action<int, string> progress)
        {
            using (FileStream fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long length = fs.Length;
                int tailSize = (int)Math.Min(length, 65557L);
                byte[] tail = new byte[tailSize];
                fs.Position = length - tailSize;
                fs.Read(tail, 0, tailSize);

                int eocd = -1;
                for (int i = tailSize - 4; i >= 0; i--)
                    if (BitConverter.ToUInt32(tail, i) == EndOfCentralDir) { eocd = i; break; }
                if (eocd < 0) throw new IOException("不是有效的 ZIP/JAR 文件：" + zipPath);

                ushort entryCount = BitConverter.ToUInt16(tail, eocd + 10);
                uint centralOffset = BitConverter.ToUInt32(tail, eocd + 16);
                if (entryCount == 0xFFFF || centralOffset == 0xFFFFFFFF)
                    throw new IOException("暂不支持 zip64 格式：" + zipPath);

                fs.Position = centralOffset;
                Directory.CreateDirectory(destDir);

                for (int index = 0; index < entryCount; index++)
                {
                    byte[] header = ReadExactly(fs, 46);
                    if (BitConverter.ToUInt32(header, 0) != CentralDirEntry)
                        throw new IOException("压缩包中央目录损坏（第 " + (index + 1) + " 项）");

                    ushort method = BitConverter.ToUInt16(header, 10);
                    uint compressedSize = BitConverter.ToUInt32(header, 20);
                    ushort nameLength = BitConverter.ToUInt16(header, 28);
                    ushort extraLength = BitConverter.ToUInt16(header, 30);
                    ushort commentLength = BitConverter.ToUInt16(header, 32);
                    uint localOffset = BitConverter.ToUInt32(header, 42);

                    string name = System.Text.Encoding.UTF8.GetString(ReadExactly(fs, nameLength));
                    if (extraLength + commentLength > 0) ReadExactly(fs, extraLength + commentLength);

                    if (filter != null && !filter(name)) continue;
                    if (name.EndsWith("/") || name.EndsWith("\\")) continue;
                    if (name.StartsWith("..") || Path.IsPathRooted(name)) continue;

                    string target = Path.Combine(destDir, name.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));

                    long resume = fs.Position;
                    fs.Position = localOffset;
                    byte[] localHeader = ReadExactly(fs, 30);
                    ushort localNameLength = BitConverter.ToUInt16(localHeader, 26);
                    ushort localExtraLength = BitConverter.ToUInt16(localHeader, 28);
                    fs.Position = localOffset + 30 + localNameLength + localExtraLength;

                    byte[] compressed = ReadExactly(fs, (int)compressedSize);
                    if (method == 0)
                    {
                        File.WriteAllBytes(target, compressed);
                    }
                    else if (method == 8)
                    {
                        using (MemoryStream memory = new MemoryStream(compressed))
                        using (DeflateStream inflater = new DeflateStream(memory, CompressionMode.Decompress))
                        using (FileStream output = new FileStream(target, FileMode.Create, FileAccess.Write))
                        {
                            byte[] buffer = new byte[8192];
                            int read;
                            while ((read = inflater.Read(buffer, 0, buffer.Length)) > 0)
                                output.Write(buffer, 0, read);
                        }
                    }
                    else
                    {
                        throw new IOException("不支持的压缩方式 " + method + "：" + name);
                    }
                    fs.Position = resume;

                    if (progress != null) progress((index + 1) * 100 / entryCount, name);
                }
            }
        }

        private static byte[] ReadExactly(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read <= 0) throw new IOException("压缩包被意外截断");
                offset += read;
            }
            return buffer;
        }
    }
}
