using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameHeatmap
{
    /// <summary>
    /// Lightweight node that only tracks move frequencies, not game data
    /// </summary>
    public class FrequencyNode
    {
        public string San { get; set; } = "";
        public int MoveNumber { get; set; }
        public bool IsWhiteMove { get; set; }
        public int Frequency { get; set; } = 0;
        public Dictionary<string, FrequencyNode> Children { get; set; } = new Dictionary<string, FrequencyNode>();

        public FrequencyNode FindOrCreateChild(string san, int moveNumber, bool isWhiteMove)
        {
            if (!Children.ContainsKey(san))
            {
                Children[san] = new FrequencyNode
                {
                    San = san,
                    MoveNumber = moveNumber,
                    IsWhiteMove = isWhiteMove
                };
            }
            return Children[san];
        }
    }

    /// <summary>
    /// Builds a lightweight frequency tree from a massive PGN file
    /// </summary>
    public class MoveFrequencyTree
    {
        public FrequencyNode Root { get; private set; }
        private int maxDepth;
        private int totalGamesProcessed = 0;

        public int TotalGamesProcessed => totalGamesProcessed;

        public MoveFrequencyTree(int maxDepth = 50)
        {
            Root = new FrequencyNode { San = "START", MoveNumber = 0, IsWhiteMove = true };
            // Double the depth since we count plies (half-moves) but users think in full moves
            this.maxDepth = maxDepth * 2;
        }

        /// <summary>
        /// Process massive PGN by chunking. Prevents memory leaks.
        /// </summary>
        // REPLACE ProcessPGNFileChunked method with this:

        // REPLACE ProcessPGNFileChunked with this version:

        public void ProcessPGNFileChunked(string filePath, int chunkSize = 50000, IProgress<(int gamesProcessed, int totalGames)>? progress = null)
        {
            System.Diagnostics.Debug.WriteLine($"[START] ProcessPGNFileChunked - ChunkSize: {chunkSize}");
            string tempDir = Path.Combine(Path.GetTempPath(), $"chess_chunks_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            System.Diagnostics.Debug.WriteLine($"[TEMPDIR] Created: {tempDir}");

            try
            {
                // STEP 1: Split file into physical chunk files on disk
                System.Diagnostics.Debug.WriteLine($"[STEP1] Starting file split...");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var chunkPgnFiles = new List<string>();
                int totalGames = 0;
                int currentChunkGames = 0;
                int chunkNumber = 0;
                StreamWriter? currentChunkWriter = null;

                System.Diagnostics.Debug.WriteLine($"[STEP1] Opening file: {filePath}");
                using (var reader = new StreamReader(filePath))
                {
                    string? line;
                    int linesRead = 0;

                    while ((line = reader.ReadLine()) != null)
                    {
                        linesRead++;
                        if (linesRead % 1000000 == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[STEP1] Read {linesRead:N0} lines, {totalGames:N0} games, {chunkPgnFiles.Count} chunks created");
                        }

                        if (line.StartsWith("[Event "))
                        {
                            // Start new chunk file if needed
                            if (currentChunkWriter == null || currentChunkGames >= chunkSize)
                            {
                                currentChunkWriter?.Close();
                                chunkNumber++;
                                string chunkFile = Path.Combine(tempDir, $"chunk_{chunkNumber:D4}.pgn");
                                chunkPgnFiles.Add(chunkFile);
                                currentChunkWriter = new StreamWriter(chunkFile);
                                currentChunkGames = 0;

                                if (chunkNumber % 10 == 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[STEP1] Created chunk file {chunkNumber}");
                                }
                            }

                            totalGames++;
                            currentChunkGames++;
                        }

                        // CRITICAL FIX: Write EVERY line (tags, blank, moves, blank)
                        currentChunkWriter?.WriteLine(line);
                    }
                }

                currentChunkWriter?.Close();

                currentChunkWriter?.Close();
                sw.Stop();
                System.Diagnostics.Debug.WriteLine($"[STEP1] COMPLETE - Split {totalGames:N0} games into {chunkPgnFiles.Count} chunk files in {sw.ElapsedMilliseconds}ms");
                System.Diagnostics.Debug.WriteLine($"[MEMORY] Before Step 2: {GC.GetTotalMemory(false) / 1024 / 1024}MB");

                // STEP 2: Process chunk files in parallel (4 at a time)
                System.Diagnostics.Debug.WriteLine($"[STEP2] Starting parallel processing of {chunkPgnFiles.Count} chunks...");
                var chunkTreeFiles = new System.Collections.Concurrent.ConcurrentBag<string>();
                int processed = 0;
                var progressLock = new object();

                System.Threading.Tasks.Parallel.ForEach(chunkPgnFiles,
                    new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 },
                    (pgnFile, state, index) =>
                    {
                        try
                        {
                            int chunkNum = (int)index + 1;
                            System.Diagnostics.Debug.WriteLine($"[STEP2-{chunkNum}] Processing chunk {chunkNum}/{chunkPgnFiles.Count}...");

                            var chunkTree = new MoveFrequencyTree(this.maxDepth / 2);
                            var parser = new PgnParser();

                            // Process chunk file line-by-line (streaming, no memory spike)
                            using (var reader = new StreamReader(pgnFile))
                            {
                                StringBuilder currentGame = new StringBuilder();
                                StringBuilder moveText = new StringBuilder();
                                string? line;
                                bool hasGameTags = false;
                                int gamesInChunk = 0;
                                int linesRead = 0;
                                bool debugFirstGame = (chunkNum == 1);

                                while ((line = reader.ReadLine()) != null)
                                {
                                    linesRead++;
                                    
                                    // Debug first few lines of first chunk
                                    if (debugFirstGame && linesRead <= 20)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[STEP2-{chunkNum}] Line {linesRead}: '{line}'");
                                    }
                                    
                                    if (currentGame.Length == 0 && string.IsNullOrWhiteSpace(line))
                                        continue;

                                    if (line.StartsWith("["))
                                    {
                                        hasGameTags = true;
                                        currentGame.AppendLine(line);
                                    }
                                    else if (!string.IsNullOrWhiteSpace(line))
                                    {
                                        moveText.Append(line);
                                        moveText.Append(" ");
                                    }
                                    else if (hasGameTags)
                                    {
                                        // Hit blank line while in game
                                        if (moveText.Length > 0)
                                        {
                                            currentGame.AppendLine();
                                            currentGame.AppendLine(moveText.ToString().Trim());
                                            moveText.Clear();
                                        }

                                        if (currentGame.Length > 0)
                                        {
                                            try
                                            {
                                                string gameText = currentGame.ToString();
                                                
                                                // Debug first game
                                                if (debugFirstGame && gamesInChunk == 0)
                                                {
                                                    System.Diagnostics.Debug.WriteLine($"[STEP2-{chunkNum}] First game text ({gameText.Length} chars):");
                                                    System.Diagnostics.Debug.WriteLine(gameText.Substring(0, Math.Min(500, gameText.Length)));
                                                }
                                                
                                                var games = parser.ParseGames(gameText);

                                                if (games.Count > 0)
                                                {
                                                    chunkTree.AddGame(games[0]);
                                                    gamesInChunk++;

                                                    lock (progressLock)
                                                    {
                                                        processed++;
                                                        if (processed % 10000 == 0)
                                                        {
                                                            progress?.Report((processed, totalGames));
                                                        }
                                                    }
                                                }
                                                else if (debugFirstGame && gamesInChunk == 0)
                                                {
                                                    System.Diagnostics.Debug.WriteLine($"[STEP2-{chunkNum}] Parser returned 0 games!");
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                if (debugFirstGame && gamesInChunk == 0)
                                                {
                                                    System.Diagnostics.Debug.WriteLine($"[STEP2-{chunkNum}] Parse error: {ex.Message}");
                                                }
                                            }
                                        }

                                        currentGame.Clear();
                                        moveText.Clear();
                                        hasGameTags = false;
                                    }
                                }
                                
                                System.Diagnostics.Debug.WriteLine($"[STEP2-{chunkNum}] Read {linesRead} lines, parsed {gamesInChunk} games");
                            }

                            // Save tree to disk
                            string treeFile = pgnFile.Replace(".pgn", ".tree");
                            chunkTree.SaveToFile(treeFile);
                            chunkTreeFiles.Add(treeFile);

                            System.Diagnostics.Debug.WriteLine($"[STEP2-{chunkNum}] COMPLETE - {chunkTree.TotalGamesProcessed:N0} games");

                            // Delete PGN chunk to save space
                            File.Delete(pgnFile);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ERROR] Chunk processing failed: {ex.Message}");
                            throw;
                        }
                    });

                System.Diagnostics.Debug.WriteLine($"[STEP2] COMPLETE - All chunks processed");
                System.Diagnostics.Debug.WriteLine($"[MEMORY] Before Step 3: {GC.GetTotalMemory(false) / 1024 / 1024}MB");

                // STEP 3: Merge all tree files
                System.Diagnostics.Debug.WriteLine($"[STEP3] Starting merge of {chunkTreeFiles.Count} tree files...");
                totalGamesProcessed = 0;

                foreach (var treeFile in chunkTreeFiles.OrderBy(f => f))
                {
                    var chunkTree = MoveFrequencyTree.LoadFromFile(treeFile);
                    if (chunkTree != null)
                    {
                        MergeTree(chunkTree);
                        totalGamesProcessed += chunkTree.TotalGamesProcessed;
                        progress?.Report((totalGamesProcessed, totalGames));
                    }
                    File.Delete(treeFile);
                }

                System.Diagnostics.Debug.WriteLine($"[STEP3] COMPLETE - Merged {totalGamesProcessed:N0} games");
                System.Diagnostics.Debug.WriteLine($"[MEMORY] Final: {GC.GetTotalMemory(false) / 1024 / 1024}MB");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FATAL ERROR] {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[FATAL ERROR] Stack: {ex.StackTrace}");
                throw;
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }
        private int CountGamesInFile(string filePath)
        {
            int count = 0;
            using (var reader = new StreamReader(filePath))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {  
                    if (line.StartsWith("[Event ")) count++;
                }
            }
            return count;
        }

        private List<long> PreComputeChunkPositions(string filePath, int chunkSize, out int totalGames)
        {
            var positions = new List<long>();
            positions.Add(0);
            totalGames = 0;
            
            using (var reader = new StreamReader(filePath))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("[Event "))
                    {
                        totalGames++;
                        if (totalGames % chunkSize == 0)
                        {
                            positions.Add(reader.BaseStream.Position);
                        }
                    }
                }
            }
            return positions;
        }

        private void ProcessChunkSimple(string filePath, int skipGames, int maxGamesToProcess, 
            MoveFrequencyTree targetTree, IProgress<(int, int)>? progress)
        {
            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            int gamesProcessed = 0;
            PgnParser parser = new PgnParser();
            
            // CRITICAL FIX: Find byte position instead of skipping games!
            long seekTime = 0;
            long startPosition = 0;
            if (skipGames > 0)
            {
                var seekSw = System.Diagnostics.Stopwatch.StartNew();
                using (var reader = new StreamReader(filePath))
                {
                    int gamesFound = 0;
                    string? line;
                    while ((line = reader.ReadLine()) != null && gamesFound < skipGames)
                    {
                        if (line.StartsWith("[Event "))
                        {
                            gamesFound++;
                        }
                    }
                    startPosition = reader.BaseStream.Position;
                }
                seekSw.Stop();
                seekTime = seekSw.ElapsedMilliseconds;
                System.Diagnostics.Debug.WriteLine($"  Seek to game {skipGames + 1}: {seekTime}ms ({seekTime / 1000.0:F1}s)");
            }
            
            // Timing breakdown
            long parseTime = 0;
            long addGameTime = 0;
            int parseCount = 0;
            
            // Now process from startPosition
            var processSw = System.Diagnostics.Stopwatch.StartNew();
            using (StreamReader reader = new StreamReader(filePath))
            {
                reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);
                reader.DiscardBufferedData();
                
                StringBuilder currentGame = new StringBuilder();
                StringBuilder moveText = new StringBuilder();
                string? line;
                bool hasGameTags = false;

                while ((line = reader.ReadLine()) != null && gamesProcessed < maxGamesToProcess)
                {
                    if (currentGame.Length == 0 && string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith("["))
                    {
                        hasGameTags = true;
                        currentGame.AppendLine(line);
                    }
                    else if (!string.IsNullOrWhiteSpace(line))
                    {
                        moveText.Append(line);
                        moveText.Append(" ");
                    }
                    else if (hasGameTags)
                    {
                        if (moveText.Length > 0)
                        {
                            currentGame.AppendLine();
                            currentGame.AppendLine(moveText.ToString().Trim());
                            moveText.Clear();
                        }

                        if (currentGame.Length > 0)
                        {
                            try
                            {
                                string gameText = currentGame.ToString();
                                
                                var parseSw = System.Diagnostics.Stopwatch.StartNew();
                                var games = parser.ParseGames(gameText);
                                parseSw.Stop();
                                parseTime += parseSw.ElapsedMilliseconds;
                                parseCount++;
                                
                                if (games.Count > 0)
                                {
                                    var addSw = System.Diagnostics.Stopwatch.StartNew();
                                    targetTree.AddGame(games[0]);
                                    addSw.Stop();
                                    addGameTime += addSw.ElapsedMilliseconds;
                                    
                                    gamesProcessed++;
                                    
                                    // Report every 100 games for faster UI updates
                                    if (gamesProcessed % 100 == 0)
                                    {
                                        progress?.Report((gamesProcessed, maxGamesToProcess));
                                    }
                                }
                            }
                            catch { }
                        }

                        currentGame.Clear();
                        moveText.Clear();
                        hasGameTags = false;
                    }
                }
                
                // Final report
                if (gamesProcessed % 100 != 0)
                {
                    progress?.Report((gamesProcessed, maxGamesToProcess));
                }
            }
            processSw.Stop();
            
            totalSw.Stop();
            double avgParse = parseCount > 0 ? parseTime / (double)parseCount : 0;
            double avgAddGame = gamesProcessed > 0 ? addGameTime / (double)gamesProcessed : 0;
            double gamesPerSec = gamesProcessed / (totalSw.ElapsedMilliseconds / 1000.0);
            long otherTime = processSw.ElapsedMilliseconds - parseTime - addGameTime;
            
            System.Diagnostics.Debug.WriteLine($"  Chunk complete: {gamesProcessed} games in {totalSw.ElapsedMilliseconds}ms ({gamesPerSec:F0} g/s)");
            System.Diagnostics.Debug.WriteLine($"    Seek: {seekTime}ms | Parse: {avgParse:F2}ms avg ({parseTime}ms total) | AddGame: {avgAddGame:F2}ms avg ({addGameTime}ms total) | Read+Other: {otherTime}ms");
        }

        private void ProcessChunkFromPosition(string filePath, long startPosition, int maxGamesToProcess, 
            MoveFrequencyTree targetTree, IProgress<(int, int)>? progress)
        {
            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            int gamesProcessed = 0;
            PgnParser parser = new PgnParser();
            
            using (StreamReader reader = new StreamReader(filePath))
            {
                reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);
                reader.DiscardBufferedData();
                
                StringBuilder currentGame = new StringBuilder();
                StringBuilder moveText = new StringBuilder();
                string? line;
                bool hasGameTags = false;

                while ((line = reader.ReadLine()) != null && gamesProcessed < maxGamesToProcess)
                {
                    if (currentGame.Length == 0 && string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith("["))
                    {
                        hasGameTags = true;
                        currentGame.AppendLine(line);
                    }
                    else if (!string.IsNullOrWhiteSpace(line))
                    {
                        moveText.Append(line);
                        moveText.Append(" ");
                    }
                    else if (hasGameTags)
                    {
                        if (moveText.Length > 0)
                        {
                            currentGame.AppendLine();
                            currentGame.AppendLine(moveText.ToString().Trim());
                            moveText.Clear();
                        }

                        if (currentGame.Length > 0)
                        {
                            try
                            {
                                string gameText = currentGame.ToString();
                                var games = parser.ParseGames(gameText);
                                
                                if (games.Count > 0)
                                {
                                    targetTree.AddGame(games[0]);
                                    gamesProcessed++;
                                    
                                    if (gamesProcessed % 100 == 0)
                                    {
                                        progress?.Report((gamesProcessed, maxGamesToProcess));
                                    }
                                }
                            }
                            catch { }
                        }

                        currentGame.Clear();
                        moveText.Clear();
                        hasGameTags = false;
                    }
                }
                
                if (gamesProcessed % 100 != 0)
                {
                    progress?.Report((gamesProcessed, maxGamesToProcess));
                }
            }
            
            totalSw.Stop();
            double gamesPerSec = gamesProcessed / (totalSw.ElapsedMilliseconds / 1000.0);
            System.Diagnostics.Debug.WriteLine($"  {gamesProcessed} games in {totalSw.ElapsedMilliseconds}ms ({gamesPerSec:F0} g/s)");
        }

        /// <summary>
        /// Process a massive PGN file with parallel processing for speed
        /// </summary>
        public void ProcessPGNFileParallel(string filePath, int maxGamesToProcess = 0, IProgress<(int gamesProcessed, int totalGames)>? progress = null, int numThreads = 4)
        {
            totalGamesProcessed = 0;

            // Step 1: Find chunk boundaries (game starts)
            var chunkBoundaries = FindChunkBoundaries(filePath, numThreads, maxGamesToProcess);
            
            // Step 2: Process each chunk in parallel - each creates multiple small tree batches
            var allTreeBatches = new List<MoveFrequencyTree>();
            var batchLock = new object();
            var progressLock = new object();
            int totalProcessed = 0;
            int sharedGameLimit = maxGamesToProcess;
            int totalBatchesCreated = 0;
            var batchStopwatch = System.Diagnostics.Stopwatch.StartNew();

            System.Threading.Tasks.Parallel.For(0, chunkBoundaries.Length - 1, i =>
            {
                var localBatches = new List<MoveFrequencyTree>();
                
                ProcessChunk(filePath, chunkBoundaries[i], chunkBoundaries[i + 1], localBatches, 
                    () => // Check if we should stop
                    {
                        lock (progressLock)
                        {
                            return sharedGameLimit > 0 && totalProcessed >= sharedGameLimit;
                        }
                    },
                    (processed) => // Report progress
                    {
                        lock (progressLock)
                        {
                            long lockAcquireTime = batchStopwatch.ElapsedMilliseconds;
                            totalProcessed += processed;
                            
                            // Debug every 10k games
                            if (totalProcessed % 10000 < processed)
                            {
                                long lockHeldTime = batchStopwatch.ElapsedMilliseconds - lockAcquireTime;
                                System.Diagnostics.Debug.WriteLine($"Progress: {totalProcessed} games | Batches: {totalBatchesCreated} | Lock held: {lockHeldTime}ms | Batch list size: {allTreeBatches.Count}");
                            }
                            
                            // UI update happens only every 5k games to reduce overhead
                            if (totalProcessed % 5000 < processed)
                            {
                                progress?.Report((totalProcessed, maxGamesToProcess));
                            }
                        }
                    },
                    batchSize: 50); // Create new tree every 50 games (keeps trees tiny)
                
                // Add all batches from this thread to the concurrent collection
                lock (batchLock)
                {
                    totalBatchesCreated += localBatches.Count;
                    foreach (var batch in localBatches)
                    {
                        allTreeBatches.Add(batch);
                    }
                }
            });

            // Step 3: Merge all tree batches one at a time
            System.Diagnostics.Debug.WriteLine($"Processing complete. Starting merge of {allTreeBatches.Count} batches...");
            totalGamesProcessed = 0;
            progress?.Report((totalProcessed, maxGamesToProcess)); // Trigger merge phase detection
            
            int batchIndex = 0;
            int totalBatches = allTreeBatches.Count;
            var mergeSw = System.Diagnostics.Stopwatch.StartNew();
            
            foreach (var treeBatch in allTreeBatches)
            {
                MergeTree(treeBatch);
                totalGamesProcessed += treeBatch.TotalGamesProcessed;
                batchIndex++;
                
                // Report merge progress every 100 batches with timing
                if (batchIndex % 100 == 0 || batchIndex == totalBatches)
                {
                    double batchesPerSec = 100.0 / (mergeSw.ElapsedMilliseconds / 1000.0);
                    System.Diagnostics.Debug.WriteLine($"Merged {batchIndex}/{totalBatches} batches ({batchesPerSec:F1} batches/sec) - {totalGamesProcessed:N0} games");
                    mergeSw.Restart();
                    progress?.Report((totalGamesProcessed, maxGamesToProcess));
                }
            }

            progress?.Report((totalGamesProcessed, maxGamesToProcess));
        }

        /// <summary>
        /// Find byte positions where games start (chunk boundaries)
        /// </summary>
        private long[] FindChunkBoundaries(string filePath, int numChunks, int maxGames)
        {
            var fileInfo = new FileInfo(filePath);
            long fileSize = fileInfo.Length;
            
            var boundaries = new List<long> { 0 }; // Start at beginning
            
            long chunkSize = fileSize / numChunks;
            
            using (var reader = new StreamReader(filePath))
            {
                for (int i = 1; i < numChunks; i++)
                {
                    // Seek to approximate chunk position
                    long targetPos = chunkSize * i;
                    reader.BaseStream.Seek(targetPos, SeekOrigin.Begin);
                    reader.DiscardBufferedData();
                    
                    // Read until we find a game start (line starting with [Event)
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("[Event "))
                        {
                            // Found a game start
                            long gameStartPos = reader.BaseStream.Position - line.Length - 2; // Account for line and newline
                            boundaries.Add(Math.Max(0, gameStartPos));
                            break;
                        }
                    }
                }
            }
            
            boundaries.Add(fileSize); // End at file end
            return boundaries.ToArray();
        }

        /// <summary>
        /// Process a chunk of the file (from startPos to endPos)
        /// Uses micro-batching: creates new tree every N games to keep trees small
        /// </summary>
        private void ProcessChunk(string filePath, long startPos, long endPos, List<MoveFrequencyTree> treeBatches, 
            Func<bool> shouldStop, Action<int> onProgress, int batchSize = 50)
        {
            int gamesProcessed = 0;
            int progressInterval = 1000; // Report every 1000 games to reduce lock contention
            int batchesCreated = 0;
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long lastReportTime = 0;
            
            // Track timing for different operations
            long totalParseTime = 0;
            long totalTreeAddTime = 0;
            long totalBatchLockTime = 0;
            int parseCount = 0;
            
            // Start with first tree
            MoveFrequencyTree currentTree = new MoveFrequencyTree(this.maxDepth / 2);
            int gamesInCurrentBatch = 0;

            using (var reader = new StreamReader(filePath))
            {
                reader.BaseStream.Seek(startPos, SeekOrigin.Begin);
                reader.DiscardBufferedData();

                StringBuilder currentGame = new StringBuilder();
                StringBuilder moveText = new StringBuilder();
                string? line;
                bool hasGameTags = false;
                int linesRead = 0;

                while (reader.BaseStream.Position < endPos && (line = reader.ReadLine()) != null)
                {
                    // Check shared game limit only occasionally (every 100 lines) to reduce lock contention
                    if (linesRead % 100 == 0 && shouldStop())
                    {
                        break;
                    }
                    
                    linesRead++;
                    
                    // Skip blank lines at start
                    if (currentGame.Length == 0 && string.IsNullOrWhiteSpace(line))
                        continue;

                    // Detect PGN tag
                    if (line.StartsWith("["))
                    {
                        hasGameTags = true;
                        currentGame.AppendLine(line);
                    }
                    // Move text
                    else if (!string.IsNullOrWhiteSpace(line))
                    {
                        moveText.Append(line);
                        moveText.Append(" ");
                    }
                    // Blank line - end of game
                    else if (hasGameTags)
                    {
                        if (moveText.Length > 0)
                        {
                            currentGame.AppendLine();
                            currentGame.AppendLine(moveText.ToString().Trim());
                            moveText.Clear();
                        }

                        if (currentGame.Length > 0)
                        {
                            try
                            {
                                string gameText = currentGame.ToString();
                                
                                // Time the parsing
                                var parseSw = System.Diagnostics.Stopwatch.StartNew();
                                PgnParser parser = new PgnParser();
                                var games = parser.ParseGames(gameText);
                                parseSw.Stop();
                                totalParseTime += parseSw.ElapsedMilliseconds;
                                parseCount++;
                                
                                if (games.Count > 0)
                                {
                                    // Time the tree add
                                    var addSw = System.Diagnostics.Stopwatch.StartNew();
                                    currentTree.AddGame(games[0]);
                                    addSw.Stop();
                                    totalTreeAddTime += addSw.ElapsedMilliseconds;
                                    
                                    gamesProcessed++;
                                    gamesInCurrentBatch++;

                                    // Check if we should start a new batch
                                    if (gamesInCurrentBatch >= batchSize)
                                    {
                                        batchesCreated++;
                                        
                                        var lockSw = System.Diagnostics.Stopwatch.StartNew();
                                        lock (treeBatches)
                                        {
                                            treeBatches.Add(currentTree);
                                        }
                                        lockSw.Stop();
                                        totalBatchLockTime += lockSw.ElapsedMilliseconds;
                                        
                                        currentTree = new MoveFrequencyTree(this.maxDepth / 2);
                                        gamesInCurrentBatch = 0;
                                    }

                                    if (gamesProcessed % progressInterval == 0)
                                    {
                                        long currentTime = sw.ElapsedMilliseconds;
                                        long intervalTime = currentTime - lastReportTime;
                                        if (gamesProcessed % 10000 == 0) // Debug every 10k games
                                        {
                                            double gamesPerSec = progressInterval / (intervalTime / 1000.0);
                                            double avgParse = parseCount > 0 ? totalParseTime / (double)parseCount : 0;
                                            double avgTreeAdd = parseCount > 0 ? totalTreeAddTime / (double)parseCount : 0;
                                            int treeSize = CountNodes(currentTree.Root);
                                            System.Diagnostics.Debug.WriteLine(
                                                $"Games {gamesProcessed}: {gamesPerSec:F0} g/s | " +
                                                $"Batches: {batchesCreated} | " +
                                                $"Parse: {avgParse:F2}ms | " +
                                                $"TreeAdd: {avgTreeAdd:F2}ms | " +
                                                $"TreeSize: {treeSize} nodes | " +
                                                $"BatchLock: {totalBatchLockTime}ms total"
                                            );
                                        }
                                        lastReportTime = currentTime;
                                        onProgress(progressInterval);
                                    }
                                }
                            }
                            catch
                            {
                                // Skip bad games
                            }
                        }

                        currentGame.Clear();
                        moveText.Clear();
                        hasGameTags = false;
                    }
                }
            }

            // Add final batch if it has games
            if (gamesInCurrentBatch > 0)
            {
                lock (treeBatches)
                {
                    treeBatches.Add(currentTree);
                }
            }

            // Report remaining games
            if (gamesProcessed % progressInterval != 0)
            {
                onProgress(gamesProcessed % progressInterval);
            }
        }

        /// <summary>
        /// Merge another tree into this one
        /// </summary>
        private void MergeTree(MoveFrequencyTree other)
        {
            MergeNodes(this.Root, other.Root);
        }

        /// <summary>
        /// Recursively merge two nodes
        /// </summary>
        private void MergeNodes(FrequencyNode target, FrequencyNode source)
        {
            target.Frequency += source.Frequency;

            foreach (var kvp in source.Children)
            {
                string san = kvp.Key;
                FrequencyNode sourceChild = kvp.Value;

                if (!target.Children.ContainsKey(san))
                {
                    // Child doesn't exist in target - just add it
                    target.Children[san] = sourceChild;
                }
                else
                {
                    // Child exists - merge recursively
                    MergeNodes(target.Children[san], sourceChild);
                }
            }
        }
        public void ProcessPGNFile(string filePath, int maxGamesToProcess = 0, IProgress<(int gamesProcessed, int totalGames)>? progress = null)
        {
            totalGamesProcessed = 0;
            PgnParser parser = new PgnParser();

            int estimatedGames = maxGamesToProcess;
            int linesRead = 0;
            int gamesAttempted = 0;

            using (StreamReader reader = new StreamReader(filePath))
            {
                StringBuilder currentGame = new StringBuilder();
                StringBuilder moveText = new StringBuilder();
                string? line;
                bool hasGameTags = false;
                bool inMoveText = false;

                while ((line = reader.ReadLine()) != null)
                {
                    linesRead++;
                    
                    // Check if we've hit the max
                    if (maxGamesToProcess > 0 && totalGamesProcessed >= maxGamesToProcess)
                        break;

                    // Skip completely blank lines at start
                    if (currentGame.Length == 0 && string.IsNullOrWhiteSpace(line))
                        continue;

                    // Detect PGN tag
                    if (line.StartsWith("["))
                    {
                        hasGameTags = true;
                        inMoveText = false;
                        currentGame.AppendLine(line);
                    }
                    // Non-tag, non-blank line (move text)
                    else if (!string.IsNullOrWhiteSpace(line))
                    {
                        inMoveText = true;
                        // Concatenate move lines with a space (moves are wrapped across lines)
                        moveText.Append(line);
                        moveText.Append(" ");
                    }
                    // Blank line - end of game
                    else if (hasGameTags)
                    {
                        // Add the complete move text to the game
                        if (moveText.Length > 0)
                        {
                            // Add a blank line between tags and moves (PGN standard)
                            currentGame.AppendLine();
                            currentGame.AppendLine(moveText.ToString().Trim());
                            moveText.Clear();
                        }
                        
                        if (currentGame.Length > 0)
                        {
                            // Process the game
                            gamesAttempted++;
                            try
                            {
                                string gameText = currentGame.ToString();
                                
                                var games = parser.ParseGames(gameText);
                                if (games.Count > 0)
                                {
                                    AddGame(games[0]);
                                    // Don't increment here - AddGame does it

                                    // Report progress every 1000 games (not every 100)
                                    if (totalGamesProcessed % 1000 == 0)
                                    {
                                        progress?.Report((totalGamesProcessed, estimatedGames));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Skip bad games silently
                            }
                        }

                        // Reset for next game
                        currentGame.Clear();
                        moveText.Clear();
                        hasGameTags = false;
                        inMoveText = false;
                    }
                }

                // Process last game if exists
                if (hasGameTags)
                {
                    // Add any remaining move text
                    if (moveText.Length > 0)
                    {
                        // Add a blank line between tags and moves (PGN standard)
                        currentGame.AppendLine();
                        currentGame.AppendLine(moveText.ToString().Trim());
                        moveText.Clear();
                    }
                    
                    if (currentGame.Length > 0)
                    {
                        gamesAttempted++;
                        try
                        {
                            string gameText = currentGame.ToString();
                            var games = parser.ParseGames(gameText);
                            if (games.Count > 0)
                            {
                                AddGame(games[0]);
                                // Don't increment here - AddGame does it
                            }
                        }
                        catch (Exception)
                        {
                            // Skip bad games silently
                        }
                    }
                }
            }

            // Debug output
            System.Diagnostics.Debug.WriteLine($"Lines read: {linesRead}, Games attempted: {gamesAttempted}, Games processed: {totalGamesProcessed}");
            progress?.Report((totalGamesProcessed, estimatedGames));
        }

        private int GetTreeDepth(FrequencyNode node, int currentDepth = 0)
        {
            if (node.Children.Count == 0)
                return currentDepth;
            
            int maxChildDepth = currentDepth;
            foreach (var child in node.Children.Values)
            {
                maxChildDepth = Math.Max(maxChildDepth, GetTreeDepth(child, currentDepth + 1));
            }
            return maxChildDepth;
        }

        private int CountNodes(FrequencyNode node)
        {
            int count = 1; // Count this node
            foreach (var child in node.Children.Values)
            {
                count += CountNodes(child);
            }
            return count;
        }

        private void AddGame(PgnGame game)
        {
            AddGameMainLine(game.MoveTreeRoot, Root, 0);
            totalGamesProcessed++; // Track games in this tree
        }

        private void AddGameMainLine(MoveNode moveNode, FrequencyNode freqNode, int depth)
        {
            // Check depth limit
            if (depth >= maxDepth)
                return;

            // Increment frequency
            freqNode.Frequency++;

            // Only follow the FIRST move (main line) - ignore variations
            if (moveNode.NextMoves.Count > 0)
            {
                var nextMove = moveNode.NextMoves[0];

                if (!string.IsNullOrEmpty(nextMove.San))
                {
                    // Determine if this is a white or black move
                    bool isWhiteMove = (depth % 2 == 0);
                    int actualMoveNumber = (depth / 2) + 1;

                    // Find or create child node
                    var childFreqNode = freqNode.FindOrCreateChild(
                        nextMove.San,
                        actualMoveNumber,
                        isWhiteMove
                    );

                    // Recursively add game through main line only
                    AddGameMainLine(nextMove, childFreqNode, depth + 1);
                }
            }
        }

        public int GetMaxFrequency()
        {
            return GetMaxFrequencyRecursive(Root);
        }

        private int GetMaxFrequencyRecursive(FrequencyNode node)
        {
            int max = node.Frequency;
            foreach (var child in node.Children.Values)
            {
                max = Math.Max(max, GetMaxFrequencyRecursive(child));
            }
            return max;
        }

        /// <summary>
        /// Save the tree to a binary file for fast loading
        /// </summary>
        public void SaveToFile(string filePath)
        {
            using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                // Write metadata
                writer.Write(maxDepth);
                writer.Write(totalGamesProcessed);
                
                // Write tree structure
                WriteNode(writer, Root);
            }
        }

        private void WriteNode(BinaryWriter writer, FrequencyNode node)
        {
            writer.Write(node.San);
            writer.Write(node.MoveNumber);
            writer.Write(node.IsWhiteMove);
            writer.Write(node.Frequency);
            writer.Write(node.Children.Count);
            
            foreach (var child in node.Children.Values)
            {
                WriteNode(writer, child);
            }
        }

        /// <summary>
        /// Load a tree from a binary file with progress reporting and cancellation support
        /// </summary>
        public static MoveFrequencyTree? LoadFromFile(string filePath, IProgress<(long bytesRead, long totalBytes)>? progress = null, Func<bool>? shouldCancel = null)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                var fileInfo = new FileInfo(filePath);
                long totalBytes = fileInfo.Length;
                
                using (var reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
                {
                    var tree = new MoveFrequencyTree();
                    
                    // Read metadata
                    tree.maxDepth = reader.ReadInt32();
                    tree.totalGamesProcessed = reader.ReadInt32();
                    
                    // Read tree structure with progress
                    int nodeCount = 0;
                    tree.Root = ReadNode(reader, ref nodeCount, totalBytes, progress, shouldCancel);
                    
                    // Clean up any CANCELLED marker nodes
                    if (tree.Root.Children.ContainsKey("CANCELLED"))
                    {
                        tree.Root.Children.Remove("CANCELLED");
                    }
                    
                    // Return tree even if cancelled (partial data)
                    return tree;
                }
            }
            catch
            {
                return null;
            }
        }

        private static FrequencyNode ReadNode(BinaryReader reader, ref int nodeCount, long totalBytes, IProgress<(long, long)>? progress = null, Func<bool>? shouldCancel = null)
        {
            // Check for cancellation every 10000 nodes
            nodeCount++;
            if (nodeCount % 10000 == 0)
            {
                if (shouldCancel != null && shouldCancel())
                {
                    // Return a valid but incomplete node when cancelled
                    return new FrequencyNode
                    {
                        San = "CANCELLED",
                        MoveNumber = 0,
                        IsWhiteMove = true,
                        Frequency = 0
                    };
                }
                    
                // Report progress every 10000 nodes based on file position
                if (progress != null)
                {
                    long bytesRead = reader.BaseStream.Position;
                    progress.Report((bytesRead, totalBytes));
                }
            }

            var node = new FrequencyNode
            {
                San = reader.ReadString(),
                MoveNumber = reader.ReadInt32(),
                IsWhiteMove = reader.ReadBoolean(),
                Frequency = reader.ReadInt32()
            };
            
            int childCount = reader.ReadInt32();
            for (int i = 0; i < childCount; i++)
            {
                var child = ReadNode(reader, ref nodeCount, totalBytes, progress, shouldCancel);
                
                // Stop reading children if we got a cancellation marker
                if (child.San == "CANCELLED")
                    break;
                    
                node.Children[child.San] = child;
            }
            
            return node;
        }
    }
}
