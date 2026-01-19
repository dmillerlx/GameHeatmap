using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GameHeatmap
{
    /// <summary>
    /// Fast offset-based navigation through binary tree blob.
    /// Entire file loaded into memory, navigation via byte offsets (no object allocation).
    /// </summary>
    public class BinaryTreeNavigator
    {
        private byte[] data;              // Entire file in memory (single file mode)
        private List<byte[]> chunks;      // Multiple chunks (multi-file mode)
        private bool isMultiFile;         // Track which mode we're in
        private string[] stringTable;     // Pre-parsed string table
        private int rootOffset;           // Offset to root node
        private int totalGames;           // Total games processed

        public int TotalGames => totalGames;
        public int RootOffset => rootOffset;

        /// <summary>
        /// Load binary blob from file (fast - just reads bytes)
        /// Supports both single-file and multi-file formats
        /// </summary>
        public void Load(string filePath, IProgress<(long bytesRead, long totalBytes)>? progress = null)
        {
            // Check if this is multi-file format (ends with .0)
            if (filePath.EndsWith(".0"))
            {
                LoadMultiFile(filePath, progress);
            }
            else
            {
                LoadSingleFile(filePath, progress);
            }
        }

        private void LoadSingleFile(string filePath, IProgress<(long bytesRead, long totalBytes)>? progress)
        {
            isMultiFile = false;

            var fileInfo = new FileInfo(filePath);
            long totalBytes = fileInfo.Length;

            // Check if file is too large for single allocation
            if (totalBytes > int.MaxValue)
            {
                throw new InvalidOperationException($"File too large ({totalBytes} bytes). Use multi-file format (.0, .1, .2...)");
            }

            // Read entire file into memory
            data = File.ReadAllBytes(filePath);

            // Parse header
            string magic = Encoding.ASCII.GetString(data, 0, 4);
            if (magic != "TREE")
                throw new InvalidDataException("Invalid blob file format");

            int version = BitConverter.ToInt32(data, 4);
            if (version != 1)
                throw new InvalidDataException($"Unsupported version: {version}");

            totalGames = BitConverter.ToInt32(data, 8);
            rootOffset = BitConverter.ToInt32(data, 12);
            int stringTableOffset = BitConverter.ToInt32(data, 16);

            // Parse string table (only strings, not nodes!)
            stringTable = ParseStringTable(stringTableOffset);

            progress?.Report((totalBytes, totalBytes));
        }

        private void LoadMultiFile(string firstChunkPath, IProgress<(long bytesRead, long totalBytes)>? progress)
        {
            isMultiFile = true;
            chunks = new List<byte[]>();

            // Get base path (remove .0)
            string basePath = firstChunkPath.Substring(0, firstChunkPath.Length - 2);

            // Load all chunks
            int chunkIndex = 0;
            long totalBytesRead = 0;
            long totalBytes = 0;

            // First pass: calculate total size
            while (File.Exists($"{basePath}.{chunkIndex}"))
            {
                totalBytes += new FileInfo($"{basePath}.{chunkIndex}").Length;
                chunkIndex++;
            }

            // Second pass: load chunks
            chunkIndex = 0;
            while (File.Exists($"{basePath}.{chunkIndex}"))
            {
                byte[] chunk = File.ReadAllBytes($"{basePath}.{chunkIndex}");
                chunks.Add(chunk);
                totalBytesRead += chunk.Length;
                progress?.Report((totalBytesRead, totalBytes));
                chunkIndex++;
            }

            if (chunks.Count == 0)
                throw new InvalidDataException("No chunk files found");

            // Parse header from first chunk
            byte[] firstChunk = chunks[0];
            string magic = Encoding.ASCII.GetString(firstChunk, 0, 4);
            if (magic != "TREE")
                throw new InvalidDataException("Invalid blob file format");

            int version = BitConverter.ToInt32(firstChunk, 4);
            if (version != 1)
                throw new InvalidDataException($"Unsupported version: {version}");

            totalGames = BitConverter.ToInt32(firstChunk, 8);
            rootOffset = BitConverter.ToInt32(firstChunk, 12);
            int stringTableOffset = BitConverter.ToInt32(firstChunk, 16);

            // Parse string table from first chunk
            stringTable = ParseStringTableMultiFile(stringTableOffset);
        }

        // Helper to read bytes from either single file or multi-file
        private byte ReadByteAt(int offset)
        {
            if (!isMultiFile)
                return data[offset];

            // Multi-file: find which chunk contains this offset
            int currentOffset = 0;
            foreach (var chunk in chunks)
            {
                if (offset < currentOffset + chunk.Length)
                {
                    return chunk[offset - currentOffset];
                }
                currentOffset += chunk.Length;
            }
            throw new IndexOutOfRangeException($"Offset {offset} out of range");
        }

        private ushort ReadUInt16At(int offset)
        {
            if (!isMultiFile)
                return BitConverter.ToUInt16(data, offset);

            // For multi-file, we need to handle case where ushort spans chunks
            byte b1 = ReadByteAt(offset);
            byte b2 = ReadByteAt(offset + 1);
            return BitConverter.ToUInt16(new byte[] { b1, b2 }, 0);
        }

        private int ReadInt32At(int offset)
        {
            if (!isMultiFile)
                return BitConverter.ToInt32(data, offset);

            byte b1 = ReadByteAt(offset);
            byte b2 = ReadByteAt(offset + 1);
            byte b3 = ReadByteAt(offset + 2);
            byte b4 = ReadByteAt(offset + 3);
            return BitConverter.ToInt32(new byte[] { b1, b2, b3, b4 }, 0);
        }

        private string ReadStringAt(int offset, int length)
        {
            if (!isMultiFile)
                return Encoding.UTF8.GetString(data, offset, length);

            // For multi-file, collect bytes across potential chunk boundaries
            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = ReadByteAt(offset + i);
            }
            return Encoding.UTF8.GetString(bytes);
        }

        private string[] ParseStringTable(int offset)
        {
            int count = BitConverter.ToUInt16(data, offset);
            var strings = new string[count];

            int pos = offset + 2;
            for (int i = 0; i < count; i++)
            {
                int length = BitConverter.ToUInt16(data, pos);
                pos += 2;

                strings[i] = Encoding.UTF8.GetString(data, pos, length);
                pos += length;
            }

            return strings;
        }

        private string[] ParseStringTableMultiFile(int offset)
        {
            int count = ReadUInt16At(offset);
            var strings = new string[count];

            int pos = offset + 2;
            for (int i = 0; i < count; i++)
            {
                int length = ReadUInt16At(pos);
                pos += 2;

                strings[i] = ReadStringAt(pos, length);
                pos += length;
            }

            return strings;
        }

        /// <summary>
        /// Get node frequency without allocating object
        /// </summary>
        public int GetFrequency(int nodeOffset)
        {
            return isMultiFile ? ReadInt32At(nodeOffset + 5) : BitConverter.ToInt32(data, nodeOffset + 5);
        }

        /// <summary>
        /// Get node SAN string without allocating
        /// </summary>
        public string GetSan(int nodeOffset)
        {
            int stringId = isMultiFile ? ReadUInt16At(nodeOffset) : BitConverter.ToUInt16(data, nodeOffset);
            return stringTable[stringId];
        }

        /// <summary>
        /// Get move number
        /// </summary>
        public int GetMoveNumber(int nodeOffset)
        {
            return isMultiFile ? ReadUInt16At(nodeOffset + 2) : BitConverter.ToUInt16(data, nodeOffset + 2);
        }

        /// <summary>
        /// Is white move?
        /// </summary>
        public bool IsWhiteMove(int nodeOffset)
        {
            byte flags = isMultiFile ? ReadByteAt(nodeOffset + 4) : data[nodeOffset + 4];
            return (flags & 0x01) != 0;
        }

        /// <summary>
        /// Get child count
        /// </summary>
        public int GetChildCount(int nodeOffset)
        {
            return isMultiFile ? ReadUInt16At(nodeOffset + 9) : BitConverter.ToUInt16(data, nodeOffset + 9);
        }

        /// <summary>
        /// Find child by move name (binary search through sorted jump table)
        /// Returns -1 if not found
        /// </summary>
        public int FindChild(int nodeOffset, string moveSan)
        {
            // Find stringId for moveSan
            int targetStringId = Array.IndexOf(stringTable, moveSan);
            if (targetStringId == -1) return -1;

            int childCount = GetChildCount(nodeOffset);
            int jumpTableStart = nodeOffset + 11;

            // Binary search (children sorted by stringId)
            int left = 0, right = childCount - 1;
            while (left <= right)
            {
                int mid = (left + right) / 2;
                int entryOffset = jumpTableStart + (mid * 6);
                int childStringId = isMultiFile ? ReadUInt16At(entryOffset) : BitConverter.ToUInt16(data, entryOffset);

                if (childStringId == targetStringId)
                {
                    return isMultiFile ? ReadInt32At(entryOffset + 2) : BitConverter.ToInt32(data, entryOffset + 2);
                }
                else if (childStringId < targetStringId)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            return -1;
        }

        /// <summary>
        /// Get all children (for display) - this DOES allocate a list
        /// </summary>
        public List<(string san, int offset, int frequency)> GetChildren(int nodeOffset)
        {
            int childCount = GetChildCount(nodeOffset);
            int jumpTableStart = nodeOffset + 11;

            var children = new List<(string, int, int)>(childCount);

            for (int i = 0; i < childCount; i++)
            {
                int entryOffset = jumpTableStart + (i * 6);
                int childStringId = isMultiFile ? ReadUInt16At(entryOffset) : BitConverter.ToUInt16(data, entryOffset);
                int childOffset = isMultiFile ? ReadInt32At(entryOffset + 2) : BitConverter.ToInt32(data, entryOffset + 2);

                string san = stringTable[childStringId];
                int freq = GetFrequency(childOffset);

                children.Add((san, childOffset, freq));
            }

            return children;
        }

        /// <summary>
        /// Materialize a FrequencyNode from offset (for compatibility with existing code)
        /// Only call this when you need an actual object (e.g., UI binding)
        /// </summary>
        public FrequencyNode MaterializeNode(int nodeOffset, bool includeChildren = true)
        {
            var node = new FrequencyNode
            {
                San = GetSan(nodeOffset),
                MoveNumber = GetMoveNumber(nodeOffset),
                IsWhiteMove = IsWhiteMove(nodeOffset),
                Frequency = GetFrequency(nodeOffset)
            };

            if (includeChildren)
            {
                foreach (var (san, offset, freq) in GetChildren(nodeOffset))
                {
                    node.Children[san] = new FrequencyNode
                    {
                        San = san,
                        Frequency = freq
                        // Don't recursively materialize - lazy load when needed
                    };
                }
            }

            return node;
        }

        /// <summary>
        /// Navigate a path like "e4.e5.Nf3" and return final offset
        /// Returns -1 if path doesn't exist
        /// </summary>
        public int NavigatePath(string path)
        {
            var moves = path.Split('.');
            int currentOffset = rootOffset;

            foreach (var move in moves)
            {
                currentOffset = FindChild(currentOffset, move);
                if (currentOffset == -1)
                    return -1;
            }

            return currentOffset;
        }
    }

    /// <summary>
    /// Builds binary blob format from MoveFrequencyTree
    /// Uses two-pass approach to eliminate offset tracking dictionary
    /// Supports multi-file output to work around 2GB .NET array limit
    /// </summary>
    public class BinaryTreeBuilder
    {
        private Dictionary<string, ushort> stringIds = new Dictionary<string, ushort>();
        private List<string> strings = new List<string>();
        private int totalGames;

        // Two-pass approach: first pass calculates offsets, second pass writes data
        private Dictionary<FrequencyNode, int> nodeOffsets = new Dictionary<FrequencyNode, int>(ReferenceEqualityComparer.Instance);

        // Multi-file support
        private const long MAX_CHUNK_SIZE = 1024L * 1024L * 1024L; // 1GB per chunk (default, overridden by parameter)
        private long maxChunkSizeBytes = MAX_CHUNK_SIZE;
        private List<FileStream> chunkStreams = new List<FileStream>();
        private List<BinaryWriter> chunkWriters = new List<BinaryWriter>();
        private int currentChunkIndex = 0;
        private long currentChunkSize = 0;

        public void BuildFromTree(MoveFrequencyTree tree, string outputPath, IProgress<(int nodesWritten, int totalNodes)>? progress = null, int maxChunkSizeMB = 1024)
        {
            Console.WriteLine($"[BLOB SAVE] Starting blob save to {outputPath}");
            Console.WriteLine($"[BLOB SAVE] Chunk size: {maxChunkSizeMB}MB ({maxChunkSizeMB * 1024L * 1024L:N0} bytes)");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Set chunk size from parameter
            maxChunkSizeBytes = maxChunkSizeMB * 1024L * 1024L;

            totalGames = tree.TotalGamesProcessed;

            // PASS 1: Calculate all offsets and collect strings
            // Note: Can't report accurate progress during counting since we don't know total yet
            // Report -1 to indicate "counting phase"
            progress?.Report((-1, -1));

            int totalNodes = CountNodes(tree.Root);

            // Now we know total - report 0 progress
            progress?.Report((0, totalNodes));

            CollectStrings(tree.Root);

            int currentOffset = 64; // After header
            int stringTableSize = CalculateStringTableSize();
            currentOffset += stringTableSize;

            int stringTableOffset = 64;
            int rootOffset = currentOffset;

            CalculateOffsets(tree.Root, ref currentOffset);

            // PASS 2: ALWAYS use chunked mode to avoid large single files
            long estimatedSize = currentOffset;
            Console.WriteLine($"[BLOB SAVE] Estimated size: {estimatedSize:N0} bytes ({estimatedSize / (1024.0 * 1024.0):F2} MB)");

            // Multi-file mode: create chunk 0
            string chunkPath = $"{outputPath}.0";
            var fileStream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
            var writer = new BinaryWriter(fileStream);
            chunkStreams.Add(fileStream);
            chunkWriters.Add(writer);

            try
            {
                // Write header to first chunk
                WriteHeader(writer, rootOffset, stringTableOffset);
                currentChunkSize = 64;

                // Write string table to first chunk
                WriteStringTable(writer);
                currentChunkSize += stringTableSize;

                // Write nodes, potentially across multiple chunks
                int nodesWritten = 0;
                WriteNodeRecursiveMultiFile(tree.Root, ref nodesWritten, totalNodes, outputPath, progress);
            }
            finally
            {
                // Close all chunk files
                foreach (var w in chunkWriters) w.Dispose();
                foreach (var s in chunkStreams) s.Dispose();
                chunkWriters.Clear();
                chunkStreams.Clear();
            }

            // Clear the offset dictionary to free memory
            nodeOffsets.Clear();

            sw.Stop();
            Console.WriteLine($"[BLOB SAVE] Complete in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds/1000.0:F1}s)");
            Console.WriteLine($"[BLOB SAVE] Total nodes: {totalNodes:N0} | Chunks: {chunkWriters.Count}");
        }

        private int CountNodes(FrequencyNode node)
        {
            int count = 1;
            foreach (var child in node.Children.Values)
            {
                count += CountNodes(child);
            }
            return count;
        }

        private void CollectStrings(FrequencyNode node)
        {
            if (!stringIds.ContainsKey(node.San))
            {
                stringIds[node.San] = (ushort)strings.Count;
                strings.Add(node.San);
            }

            // Recursively collect strings from children first
            foreach (var child in node.Children.Values)
            {
                CollectStrings(child);
            }

            // NOW that all string IDs are assigned, we can sort and cache children
            if (node.Children.Count > 0)
            {
                node.SortedChildrenCache = node.Children.Values.OrderBy(c => stringIds[c.San]).ToList();
            }
        }

        private int CalculateStringTableSize()
        {
            int size = 2; // Count field
            foreach (var str in strings)
            {
                size += 2; // Length field
                size += Encoding.UTF8.GetByteCount(str);
            }
            return size;
        }

        private void CalculateOffsets(FrequencyNode node, ref int currentOffset)
        {
            // Store this node's offset
            nodeOffsets[node] = currentOffset;

            // Calculate size of this node
            int nodeSize = 11; // Header: stringId(2) + moveNum(2) + flags(1) + freq(4) + childCount(2)
            nodeSize += node.Children.Count * 6; // Jump table: (stringId(2) + offset(4)) per child

            currentOffset += nodeSize;

            // Use pre-sorted children from cache (sorted during CollectStrings)
            if (node.SortedChildrenCache != null)
            {
                foreach (var child in node.SortedChildrenCache)
                {
                    CalculateOffsets(child, ref currentOffset);
                }
            }
        }

        private void WriteHeader(BinaryWriter writer, int rootOffset, int stringTableOffset)
        {
            writer.Write(Encoding.ASCII.GetBytes("TREE")); // Magic number
            writer.Write(1);                                // Version
            writer.Write(totalGames);                       // Total games
            writer.Write(rootOffset);                       // Root node offset
            writer.Write(stringTableOffset);                // String table offset
            writer.Write(new byte[44]);                     // Padding to 64 bytes
        }

        private void WriteStringTable(BinaryWriter writer)
        {
            writer.Write((ushort)strings.Count);

            foreach (var str in strings)
            {
                var bytes = Encoding.UTF8.GetBytes(str);
                writer.Write((ushort)bytes.Length);
                writer.Write(bytes);
            }
        }

        private void WriteNodeRecursive(BinaryWriter writer, FrequencyNode node, ref int nodesWritten, int totalNodes, IProgress<(int, int)>? progress)
        {
            // Increment counter first
            nodesWritten++;

            // Write node header
            writer.Write(stringIds[node.San]);              // 2 bytes - string ID
            writer.Write((ushort)node.MoveNumber);          // 2 bytes - move number
            byte flags = (byte)(node.IsWhiteMove ? 0x01 : 0x00);
            writer.Write(flags);                            // 1 byte - flags
            writer.Write(node.Frequency);                   // 4 bytes - frequency
            writer.Write((ushort)node.Children.Count);      // 2 bytes - child count

            // Use pre-sorted children from cache
            var sortedChildren = node.SortedChildrenCache ?? new List<FrequencyNode>();

            // Write jump table with pre-calculated offsets (no seeking needed!)
            foreach (var child in sortedChildren)
            {
                writer.Write(stringIds[child.San]);         // 2 bytes - string ID
                writer.Write(nodeOffsets[child]);           // 4 bytes - pre-calculated offset
            }

            // Report progress periodically
            if (nodesWritten % 10000 == 0)
            {
                progress?.Report((nodesWritten, totalNodes));
            }

            // Write children recursively (in same order)
            foreach (var child in sortedChildren)
            {
                WriteNodeRecursive(writer, child, ref nodesWritten, totalNodes, progress);
            }
        }

        private void WriteNodeRecursiveMultiFile(FrequencyNode node, ref int nodesWritten, int totalNodes, string baseOutputPath, IProgress<(int, int)>? progress)
        {
            // Check if we need to start a new chunk BEFORE writing this node
            // Calculate this node's size
            int nodeSize = 11 + (node.Children.Count * 6);

            if (currentChunkSize + nodeSize > maxChunkSizeBytes && currentChunkIndex < 999)
            {
                // Start a new chunk
                currentChunkIndex++;
                currentChunkSize = 0;

                string chunkPath = $"{baseOutputPath}.{currentChunkIndex}";
                var fileStream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
                var writer = new BinaryWriter(fileStream);
                chunkStreams.Add(fileStream);
                chunkWriters.Add(writer);
            }

            // Write to current chunk
            var currentWriter = chunkWriters[currentChunkIndex];

            // Increment counter
            nodesWritten++;

            // Write node header
            currentWriter.Write(stringIds[node.San]);              // 2 bytes - string ID
            currentWriter.Write((ushort)node.MoveNumber);          // 2 bytes - move number
            byte flags = (byte)(node.IsWhiteMove ? 0x01 : 0x00);
            currentWriter.Write(flags);                            // 1 byte - flags
            currentWriter.Write(node.Frequency);                   // 4 bytes - frequency
            currentWriter.Write((ushort)node.Children.Count);      // 2 bytes - child count

            // Use pre-sorted children from cache
            var sortedChildren = node.SortedChildrenCache ?? new List<FrequencyNode>();

            // Write jump table
            foreach (var child in sortedChildren)
            {
                currentWriter.Write(stringIds[child.San]);         // 2 bytes - string ID
                currentWriter.Write(nodeOffsets[child]);           // 4 bytes - pre-calculated offset
            }

            currentChunkSize += nodeSize;

            // Report progress periodically
            if (nodesWritten % 10000 == 0)
            {
                progress?.Report((nodesWritten, totalNodes));
            }

            // Write children recursively
            foreach (var child in sortedChildren)
            {
                WriteNodeRecursiveMultiFile(child, ref nodesWritten, totalNodes, baseOutputPath, progress);
            }
        }
    }
}
