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
        private List<byte[]> chunks;      // Multiple chunks in memory
        private string[] stringTable;     // Pre-parsed string table
        private long rootOffset;          // Offset to root node (long for >2GB support)
        private int totalGames;           // Total games processed
        private int version;              // File format version

        public int TotalGames => totalGames;
        public long RootOffset => rootOffset;

        /// <summary>
        /// Load binary blob from multi-file format (fast - just reads bytes)
        /// </summary>
        public void Load(string filePath, IProgress<(long bytesRead, long totalBytes, int currentChunk, int totalChunks)>? progress = null)
        {
            // Ensure this is multi-file format (ends with .0)
            if (!filePath.EndsWith(".0"))
            {
                throw new InvalidOperationException("Only multi-file format is supported. File path must end with .0");
            }

            LoadMultiFile(filePath, progress);
        }

        private void LoadMultiFile(string firstChunkPath, IProgress<(long bytesRead, long totalBytes, int currentChunk, int totalChunks)>? progress)
        {
            chunks = new List<byte[]>();

            // Get base path (remove .0)
            string basePath = firstChunkPath.Substring(0, firstChunkPath.Length - 2);

            // Load all chunks
            int chunkIndex = 0;
            long totalBytesRead = 0;
            long totalBytes = 0;

            // First pass: calculate total size and count chunks
            int totalChunks = 0;
            while (File.Exists($"{basePath}.{chunkIndex}"))
            {
                totalBytes += new FileInfo($"{basePath}.{chunkIndex}").Length;
                chunkIndex++;
                totalChunks++;
            }

            // Second pass: load chunks
            chunkIndex = 0;
            while (File.Exists($"{basePath}.{chunkIndex}"))
            {
                byte[] chunk = File.ReadAllBytes($"{basePath}.{chunkIndex}");
                chunks.Add(chunk);
                totalBytesRead += chunk.Length;
                chunkIndex++;
                progress?.Report((totalBytesRead, totalBytes, chunkIndex, totalChunks));
            }

            if (chunks.Count == 0)
                throw new InvalidDataException("No chunk files found");

            // Parse header from first chunk
            byte[] firstChunk = chunks[0];
            string magic = Encoding.ASCII.GetString(firstChunk, 0, 4);
            if (magic != "TREE")
                throw new InvalidDataException("Invalid blob file format");

            version = BitConverter.ToInt32(firstChunk, 4);
            if (version != 1 && version != 2)
                throw new InvalidDataException($"Unsupported version: {version}");

            totalGames = BitConverter.ToInt32(firstChunk, 8);

            if (version == 1)
            {
                // Version 1: 4-byte offsets
                rootOffset = BitConverter.ToInt32(firstChunk, 12);
            }
            else
            {
                // Version 2: 8-byte offsets
                rootOffset = BitConverter.ToInt64(firstChunk, 12);
            }

            int stringTableOffset = BitConverter.ToInt32(firstChunk, version == 1 ? 16 : 20);

            // Parse string table from first chunk
            stringTable = ParseStringTableMultiFile(stringTableOffset);
        }

        // Helper to read bytes from multi-file chunks
        private byte ReadByteAt(long offset)
        {
            if (offset < 0)
            {
                throw new IndexOutOfRangeException($"Offset {offset} is negative");
            }

            // Find which chunk contains this offset
            long currentOffset = 0;
            long totalSize = 0;
            foreach (var chunk in chunks)
            {
                totalSize += chunk.Length;
                if (offset < currentOffset + chunk.Length)
                {
                    // Calculate position within this chunk
                    long positionInChunk = offset - currentOffset;

                    // Validate it fits in an int (chunk arrays are limited to int.MaxValue)
                    if (positionInChunk > int.MaxValue)
                    {
                        throw new IndexOutOfRangeException($"Position {positionInChunk} within chunk exceeds maximum array size");
                    }

                    return chunk[(int)positionInChunk];
                }
                currentOffset += chunk.Length;
            }
            throw new IndexOutOfRangeException($"Offset {offset} out of range. Total file size: {totalSize} bytes, {chunks.Count} chunks. This likely indicates a corrupt blob file where a child offset points past the end of the file.");
        }

        private ushort ReadUInt16At(long offset)
        {
            // Handle case where ushort spans chunks
            byte b1 = ReadByteAt(offset);
            byte b2 = ReadByteAt(offset + 1);
            return BitConverter.ToUInt16(new byte[] { b1, b2 }, 0);
        }

        private int ReadInt32At(long offset)
        {
            byte b1 = ReadByteAt(offset);
            byte b2 = ReadByteAt(offset + 1);
            byte b3 = ReadByteAt(offset + 2);
            byte b4 = ReadByteAt(offset + 3);
            return BitConverter.ToInt32(new byte[] { b1, b2, b3, b4 }, 0);
        }

        private long ReadInt64At(long offset)
        {
            byte[] bytes = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                bytes[i] = ReadByteAt(offset + i);
            }
            return BitConverter.ToInt64(bytes, 0);
        }

        private string ReadStringAt(int offset, int length)
        {
            // Collect bytes across potential chunk boundaries
            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = ReadByteAt(offset + i);
            }
            return Encoding.UTF8.GetString(bytes);
        }

        private string[] ParseStringTableMultiFile(int offset)
        {
            int count = ReadUInt16At(offset);
            var strings = new string[count];

            System.Diagnostics.Debug.WriteLine($"[STRING TABLE] Reading {count} strings from offset {offset}");

            int pos = offset + 2;
            for (int i = 0; i < count; i++)
            {
                int length = ReadUInt16At(pos);
                pos += 2;

                if (i % 1000 == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[STRING TABLE] Reading string {i}/{count}, pos={pos}, length={length}");
                }

                try
                {
                    strings[i] = ReadStringAt(pos, length);
                    pos += length;
                }
                catch (IndexOutOfRangeException ex)
                {
                    throw new InvalidDataException($"Error reading string {i}/{count} at position {pos} with length {length}. Total file size: {chunks.Sum(c => c.Length)} bytes. This suggests the string table is corrupted or incomplete.", ex);
                }
            }

            System.Diagnostics.Debug.WriteLine($"[STRING TABLE] Finished reading {count} strings, final pos={pos}");

            return strings;
        }

        /// <summary>
        /// Get node frequency without allocating object
        /// </summary>
        public int GetFrequency(long nodeOffset)
        {
            return ReadInt32At(nodeOffset + 5);
        }

        /// <summary>
        /// Get node SAN string without allocating
        /// </summary>
        public string GetSan(long nodeOffset)
        {
            int stringId = ReadUInt16At(nodeOffset);
            return stringTable[stringId];
        }

        /// <summary>
        /// Get move number
        /// </summary>
        public int GetMoveNumber(long nodeOffset)
        {
            return ReadUInt16At(nodeOffset + 2);
        }

        /// <summary>
        /// Is white move?
        /// </summary>
        public bool IsWhiteMove(long nodeOffset)
        {
            byte flags = ReadByteAt(nodeOffset + 4);
            return (flags & 0x01) != 0;
        }

        /// <summary>
        /// Get child count
        /// </summary>
        public int GetChildCount(long nodeOffset)
        {
            return ReadUInt16At(nodeOffset + 9);
        }

        /// <summary>
        /// Find child by move name (binary search through sorted jump table)
        /// Returns -1 if not found
        /// </summary>
        public long FindChild(long nodeOffset, string moveSan)
        {
            // Find stringId for moveSan
            int targetStringId = Array.IndexOf(stringTable, moveSan);
            if (targetStringId == -1) return -1;

            int childCount = GetChildCount(nodeOffset);
            long jumpTableStart = nodeOffset + 11;
            int entrySize = version == 1 ? 6 : 10; // Version 1: 2+4 bytes, Version 2: 2+8 bytes

            // Binary search (children sorted by stringId)
            int left = 0, right = childCount - 1;
            while (left <= right)
            {
                int mid = (left + right) / 2;
                long entryOffset = jumpTableStart + (mid * entrySize);
                int childStringId = ReadUInt16At(entryOffset);

                // Validate childStringId
                if (childStringId < 0 || childStringId >= stringTable.Length)
                {
                    throw new InvalidDataException($"Invalid string ID {childStringId} at offset {entryOffset}. String table has {stringTable.Length} entries. This may indicate a corrupted blob file or version mismatch.");
                }

                if (childStringId == targetStringId)
                {
                    if (version == 1)
                    {
                        return ReadInt32At(entryOffset + 2);
                    }
                    else
                    {
                        return ReadInt64At(entryOffset + 2);
                    }
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
        public List<(string san, long offset, int frequency)> GetChildren(long nodeOffset)
        {
            int childCount = GetChildCount(nodeOffset);
            long jumpTableStart = nodeOffset + 11;
            int entrySize = version == 1 ? 6 : 10;

            var children = new List<(string, long, int)>(childCount);

            for (int i = 0; i < childCount; i++)
            {
                long entryOffset = jumpTableStart + (i * entrySize);
                int childStringId = ReadUInt16At(entryOffset);
                long childOffset;

                if (version == 1)
                {
                    childOffset = ReadInt32At(entryOffset + 2);
                }
                else
                {
                    childOffset = ReadInt64At(entryOffset + 2);
                }

                // Validate childStringId to prevent crashes from corrupted data
                if (childStringId < 0 || childStringId >= stringTable.Length)
                {
                    throw new InvalidDataException($"Invalid string ID {childStringId} at offset {entryOffset}. String table has {stringTable.Length} entries. This may indicate a corrupted blob file or version mismatch.");
                }

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
        public FrequencyNode MaterializeNode(long nodeOffset, bool includeChildren = true)
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
        public long NavigatePath(string path)
        {
            var moves = path.Split('.');
            long currentOffset = rootOffset;

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
        private Dictionary<FrequencyNode, long> nodeOffsets = new Dictionary<FrequencyNode, long>(ReferenceEqualityComparer.Instance);

        // Multi-file support
        private const long MAX_CHUNK_SIZE = 1024L * 1024L * 1024L; // 1GB per chunk (default, overridden by parameter)
        private long maxChunkSizeBytes = MAX_CHUNK_SIZE;
        private List<FileStream> chunkStreams = new List<FileStream>();
        private List<BinaryWriter> chunkWriters = new List<BinaryWriter>();
        private int currentChunkIndex = 0;
        private long currentChunkSize = 0;

        public void BuildFromTree(MoveFrequencyTree tree, string outputPath, IProgress<(int nodesWritten, int totalNodes)>? progress = null, int maxChunkSizeMB = 1024)
        {
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Starting blob save to {outputPath}");
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Chunk size: {maxChunkSizeMB}MB ({maxChunkSizeMB * 1024L * 1024L:N0} bytes)");
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Starting blob save to {outputPath}");
            var totalSw = System.Diagnostics.Stopwatch.StartNew();

            // Set chunk size from parameter
            maxChunkSizeBytes = maxChunkSizeMB * 1024L * 1024L;

            totalGames = tree.TotalGamesProcessed;

            // PHASE 1: Count nodes in parallel
            progress?.Report((-1, -1));
            var phase1Sw = System.Diagnostics.Stopwatch.StartNew();

            int totalNodes = CountNodesParallel(tree.Root);

            phase1Sw.Stop();
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Phase 1 (Count): {totalNodes:N0} nodes in {phase1Sw.ElapsedMilliseconds}ms ({phase1Sw.ElapsedMilliseconds/1000.0:F1}s)");
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Phase 1 (Count): {totalNodes:N0} nodes in {phase1Sw.ElapsedMilliseconds}ms ({phase1Sw.ElapsedMilliseconds/1000.0:F1}s)");

            // Now we know total - report 0 progress
            progress?.Report((0, totalNodes));

            // PHASE 2: Collect strings in parallel
            var phase2Sw = System.Diagnostics.Stopwatch.StartNew();

            CollectStringsParallel(tree.Root);

            phase2Sw.Stop();
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Phase 2 (Strings): {strings.Count:N0} unique strings in {phase2Sw.ElapsedMilliseconds}ms ({phase2Sw.ElapsedMilliseconds/1000.0:F1}s)");
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Phase 2 (Strings): {strings.Count:N0} unique strings in {phase2Sw.ElapsedMilliseconds}ms ({phase2Sw.ElapsedMilliseconds/1000.0:F1}s)");

            // PHASE 3: Calculate offsets (still sequential for now)
            var phase3Sw = System.Diagnostics.Stopwatch.StartNew();

            long currentOffset = 64; // After header
            int stringTableSize = CalculateStringTableSize();
            currentOffset += stringTableSize;

            int stringTableOffset = 64;
            long rootOffset = currentOffset;

            CalculateOffsets(tree.Root, ref currentOffset);

            phase3Sw.Stop();
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Phase 3 (Offsets): Calculated in {phase3Sw.ElapsedMilliseconds}ms ({phase3Sw.ElapsedMilliseconds/1000.0:F1}s)");
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Phase 3 (Offsets): Calculated in {phase3Sw.ElapsedMilliseconds}ms ({phase3Sw.ElapsedMilliseconds/1000.0:F1}s)");

            // PHASE 4: Write to disk
            long estimatedSize = currentOffset;
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Estimated size: {estimatedSize:N0} bytes ({estimatedSize / (1024.0 * 1024.0):F2} MB)");
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Estimated size: {estimatedSize:N0} bytes ({estimatedSize / (1024.0 * 1024.0):F2} MB)");

            var phase4Sw = System.Diagnostics.Stopwatch.StartNew();

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

            phase4Sw.Stop();
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Phase 4 (Write): {totalNodes:N0} nodes written in {phase4Sw.ElapsedMilliseconds}ms ({phase4Sw.ElapsedMilliseconds/1000.0:F1}s)");
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Phase 4 (Write): {totalNodes:N0} nodes written in {phase4Sw.ElapsedMilliseconds}ms ({phase4Sw.ElapsedMilliseconds/1000.0:F1}s)");

            // Clear the offset dictionary to free memory
            nodeOffsets.Clear();

            totalSw.Stop();
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] ===== COMPLETE =====");
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Total time: {totalSw.ElapsedMilliseconds}ms ({totalSw.ElapsedMilliseconds/1000.0:F1}s)");
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Phase 1: {phase1Sw.ElapsedMilliseconds}ms | Phase 2: {phase2Sw.ElapsedMilliseconds}ms | Phase 3: {phase3Sw.ElapsedMilliseconds}ms | Phase 4: {phase4Sw.ElapsedMilliseconds}ms");
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Total nodes: {totalNodes:N0} | Chunks: {chunkStreams.Count}");
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] ===== COMPLETE =====");
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Total time: {totalSw.ElapsedMilliseconds}ms ({totalSw.ElapsedMilliseconds/1000.0:F1}s)");
            System.Diagnostics.Debug.WriteLine($"[BLOB SAVE] Phase 1: {phase1Sw.ElapsedMilliseconds}ms | Phase 2: {phase2Sw.ElapsedMilliseconds}ms | Phase 3: {phase3Sw.ElapsedMilliseconds}ms | Phase 4: {phase4Sw.ElapsedMilliseconds}ms");
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

        private int CountNodesParallel(FrequencyNode root)
        {
            // Get immediate children of root (these are independent subtrees)
            var rootChildren = root.Children.Values.ToList();

            if (rootChildren.Count == 0)
            {
                return 1; // Just the root
            }

            // Count each subtree in parallel
            int[] subtreeCounts = new int[rootChildren.Count];

            System.Threading.Tasks.Parallel.For(0, rootChildren.Count, i =>
            {
                subtreeCounts[i] = CountNodes(rootChildren[i]);
            });

            // Sum all subtree counts + 1 for root
            int total = 1; // Root itself
            for (int i = 0; i < subtreeCounts.Length; i++)
            {
                total += subtreeCounts[i];
            }

            return total;
        }

        private void CollectStrings(FrequencyNode node)
        {
            if (!stringIds.ContainsKey(node.San))
            {
                if (strings.Count >= ushort.MaxValue)
                {
                    throw new InvalidOperationException($"String table overflow: Cannot have more than {ushort.MaxValue} unique strings in blob format. Current count: {strings.Count}");
                }
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

        private void CollectStringsParallel(FrequencyNode root)
        {
            // Step 1: Collect unique strings from all subtrees in parallel (into local collections)
            var rootChildren = root.Children.Values.ToList();

            // Add root's own string first
            if (!stringIds.ContainsKey(root.San))
            {
                if (strings.Count >= ushort.MaxValue)
                {
                    throw new InvalidOperationException($"String table overflow: Cannot have more than {ushort.MaxValue} unique strings in blob format. Current count: {strings.Count}");
                }
                stringIds[root.San] = (ushort)strings.Count;
                strings.Add(root.San);
            }

            if (rootChildren.Count == 0)
            {
                return; // No children to process
            }

            // Each thread collects strings into its own HashSet
            var localStringSets = new System.Collections.Concurrent.ConcurrentBag<HashSet<string>>();

            System.Threading.Tasks.Parallel.ForEach(rootChildren, child =>
            {
                var localStrings = new HashSet<string>();
                CollectStringsLocal(child, localStrings);
                localStringSets.Add(localStrings);
            });

            // Step 2: Merge all local string sets into global stringIds (sequential, but fast)
            var allUniqueStrings = new HashSet<string>();
            foreach (var localSet in localStringSets)
            {
                allUniqueStrings.UnionWith(localSet);
            }

            // Remove strings we already have
            allUniqueStrings.ExceptWith(stringIds.Keys);

            // Add new strings to global list
            foreach (var str in allUniqueStrings.OrderBy(s => s)) // Sort for consistency
            {
                if (strings.Count >= ushort.MaxValue)
                {
                    throw new InvalidOperationException($"String table overflow: Cannot have more than {ushort.MaxValue} unique strings in blob format. Current count: {strings.Count}");
                }
                stringIds[str] = (ushort)strings.Count;
                strings.Add(str);
            }

            // Step 3: Now that all string IDs are assigned, sort children in parallel
            System.Threading.Tasks.Parallel.ForEach(rootChildren, child =>
            {
                SortChildrenRecursive(child);
            });

            // Sort root's children too
            if (root.Children.Count > 0)
            {
                root.SortedChildrenCache = root.Children.Values.OrderBy(c => stringIds[c.San]).ToList();
            }
        }

        private void CollectStringsLocal(FrequencyNode node, HashSet<string> localStrings)
        {
            // Collect this node's string
            localStrings.Add(node.San);

            // Recursively collect from children
            foreach (var child in node.Children.Values)
            {
                CollectStringsLocal(child, localStrings);
            }
        }

        private void SortChildrenRecursive(FrequencyNode node)
        {
            // Sort this node's children
            if (node.Children.Count > 0)
            {
                node.SortedChildrenCache = node.Children.Values.OrderBy(c => stringIds[c.San]).ToList();

                // Recursively sort children's children
                foreach (var child in node.Children.Values)
                {
                    SortChildrenRecursive(child);
                }
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

        private void CalculateOffsets(FrequencyNode node, ref long currentOffset)
        {
            // Store this node's offset
            nodeOffsets[node] = currentOffset;

            // Calculate size of this node
            long nodeSize = 11; // Header: stringId(2) + moveNum(2) + flags(1) + freq(4) + childCount(2)

            // Validate that SortedChildrenCache matches Children count
            int childCount = node.SortedChildrenCache?.Count ?? node.Children.Count;
            if (node.SortedChildrenCache != null && node.SortedChildrenCache.Count != node.Children.Count)
            {
                throw new InvalidOperationException($"Mismatch between Children.Count ({node.Children.Count}) and SortedChildrenCache.Count ({node.SortedChildrenCache.Count}) for node '{node.San}'");
            }

            nodeSize += childCount * 10; // Jump table: (stringId(2) + offset(8)) per child

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

        private void WriteHeader(BinaryWriter writer, long rootOffset, int stringTableOffset)
        {
            writer.Write(Encoding.ASCII.GetBytes("TREE")); // Magic number - 4 bytes
            writer.Write(2);                                // Version 2 - 4 bytes
            writer.Write(totalGames);                       // Total games - 4 bytes
            writer.Write(rootOffset);                       // Root node offset - 8 bytes
            writer.Write(stringTableOffset);                // String table offset - 4 bytes
            writer.Write(new byte[40]);                     // Padding to 64 bytes total (4+4+4+8+4+40=64)
        }

        private void WriteStringTable(BinaryWriter writer)
        {
            long startPos = writer.BaseStream.Position;
            System.Diagnostics.Debug.WriteLine($"[STRING TABLE WRITE] Writing {strings.Count} strings at position {startPos}");

            writer.Write((ushort)strings.Count);

            int totalBytesWritten = 2; // For the count field
            foreach (var str in strings)
            {
                var bytes = Encoding.UTF8.GetBytes(str);
                writer.Write((ushort)bytes.Length);
                writer.Write(bytes);
                totalBytesWritten += 2 + bytes.Length;
            }

            long endPos = writer.BaseStream.Position;
            System.Diagnostics.Debug.WriteLine($"[STRING TABLE WRITE] Finished at position {endPos}, wrote {totalBytesWritten} bytes total");
        }

        private void WriteNodeRecursiveMultiFile(FrequencyNode node, ref int nodesWritten, int totalNodes, string baseOutputPath, IProgress<(int, int)>? progress)
        {
            // Calculate this node's size
            int nodeSize = 11 + (node.Children.Count * 10); // Header(11) + jump table entries (stringId(2) + offset(8)) per child

            // Check if we need to start a new chunk BEFORE writing this node
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
            var sortedChildren = node.SortedChildrenCache;

            // Validate and write children
            if (node.Children.Count > 0)
            {
                if (sortedChildren == null)
                {
                    throw new InvalidOperationException($"Node '{node.San}' (MoveNum={node.MoveNumber}, Freq={node.Frequency}) has {node.Children.Count} children but SortedChildrenCache is null. This indicates the CollectStrings phase didn't run properly. First child: '{node.Children.Values.First().San}'");
                }

                // Write jump table with pre-calculated offsets
                foreach (var child in sortedChildren)
                {
                    if (child == null)
                    {
                        throw new InvalidOperationException($"Null child found in SortedChildrenCache for node '{node.San}'");
                    }

                    currentWriter.Write(stringIds[child.San]);         // 2 bytes - string ID
                    currentWriter.Write(nodeOffsets[child]);           // 8 bytes - pre-calculated offset (long for >2GB support)
                }
            }

            currentChunkSize += nodeSize;

            // Report progress periodically
            if (nodesWritten % 10000 == 0)
            {
                progress?.Report((nodesWritten, totalNodes));
            }

            // Write children recursively
            if (sortedChildren != null)
            {
                foreach (var child in sortedChildren)
                {
                    WriteNodeRecursiveMultiFile(child, ref nodesWritten, totalNodes, baseOutputPath, progress);
                }
            }
        }
    }
}
