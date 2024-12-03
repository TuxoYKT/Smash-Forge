using SALT.Moveset;
using SmashForge;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoonSharp.Interpreter;
using System.Collections.Concurrent;
using System.Threading;
using System.IO.Compression;

namespace WanganTools
{
    internal class Program
    {
        // Your existing ModelSection and FileEntry classes remain the same
        public class ModelSection
        {
            public int SectionId { get; set; }
            public string BinPath { get; set; }
            public List<FileEntry> LongFiles { get; set; }
            public List<FileEntry> NearFiles { get; set; }
            public List<FileEntry> LodmFiles { get; set; }
            public List<FileEntry> RoadFiles { get; set; }
            public List<FileEntry> OnrdFiles { get; set; }
            public List<FileEntry> BackFiles { get; set; }
            public List<FileEntry> CastFiles { get; set; }
            public List<FileEntry> RefcFiles { get; set; }
            public List<FileEntry> RefrFiles { get; set; }
            public List<FileEntry> RfbgFiles { get; set; }

            public ModelSection()
            {
                LongFiles = new List<FileEntry>();
                NearFiles = new List<FileEntry>();
                LodmFiles = new List<FileEntry>();
                RoadFiles = new List<FileEntry>();
                OnrdFiles = new List<FileEntry>();
                BackFiles = new List<FileEntry>();
                CastFiles = new List<FileEntry>();
                RefcFiles = new List<FileEntry>();
                RefrFiles = new List<FileEntry>();
                RfbgFiles = new List<FileEntry>();
            }
        }

        public class FileEntry
        {
            public string Name { get; set; }
            public long StartAddress { get; set; }
            public long Length { get; set; }
        }

        public class LuaModelParser
        {
            private readonly Script _script;

            public LuaModelParser()
            {
                _script = new Script();
            }

            public List<string> ParseTextureList(string luaContent)
            {
                _script.DoString(luaContent);
                var textureList = _script.Globals.Get("TEXTURELIST");
                var results = new List<string>();

                foreach (var texture in textureList.Table.Values)
                {
                    results.Add(texture.String);
                }

                return results;
            }

            public List<ModelSection> ParseModelList(string luaContent)
            {
                _script.DoString(luaContent);
                var modelList = _script.Globals.Get("MODELLIST");
                var results = new List<ModelSection>();

                foreach (var sectionData in modelList.Table.Values)
                {
                    var section = new ModelSection
                    {
                        SectionId = (int)sectionData.Table.Get("SECTION_ID").Number,
                        BinPath = sectionData.Table.Get("BIN").String
                    };

                    ParseFileEntries(sectionData.Table, "LONG", section.LongFiles);
                    ParseFileEntries(sectionData.Table, "NEAR", section.NearFiles);
                    ParseFileEntries(sectionData.Table, "LODM", section.LodmFiles);
                    ParseFileEntries(sectionData.Table, "ROAD", section.RoadFiles);
                    ParseFileEntries(sectionData.Table, "ONRD", section.OnrdFiles);
                    ParseFileEntries(sectionData.Table, "BACK", section.BackFiles);
                    ParseFileEntries(sectionData.Table, "CAST", section.CastFiles);
                    ParseFileEntries(sectionData.Table, "REFC", section.RefcFiles);
                    ParseFileEntries(sectionData.Table, "REFR", section.RefrFiles);
                    ParseFileEntries(sectionData.Table, "RFBG", section.RfbgFiles);

                    results.Add(section);
                }

                return results;
            }

            private void ParseFileEntries(Table sectionTable, string prefix, List<FileEntry> entries)
            {
                var addresses = sectionTable.Get($"{prefix}_ADDR").Table;
                var names = sectionTable.Get($"{prefix}_NAME").Table;

                var addressList = new List<long>();
                foreach (var addr in addresses.Values)
                {
                    addressList.Add((long)addr.Number);
                }

                var nameList = new List<string>();
                foreach (var name in names.Values)
                {
                    nameList.Add(name.String);
                }

                for (int i = 0; i < nameList.Count; i++)
                {
                    entries.Add(new FileEntry
                    {
                        Name = nameList[i],
                        StartAddress = addressList[i * 2],
                        Length = addressList[i * 2 + 1]
                    });
                }
            }
        }

        public class BinaryCache : IDisposable
        {
            private byte[] decompressedData;
            private readonly string filePath;
            private readonly bool isCompressed;
            private bool isDisposed;

            public BinaryCache(string filePath)
            {
                this.filePath = filePath;
                this.isCompressed = IsGZipCompressed(filePath);
                LoadFile();
            }

            private static bool IsGZipCompressed(string filePath)
            {
                try
                {
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        if (fs.Length < 2)
                            return false;

                        byte[] signature = new byte[2];
                        fs.Read(signature, 0, 2);

                        return signature[0] == 0x1F && signature[1] == 0x8B;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }

            private void LoadFile()
            {
                if (isCompressed)
                {
                    using (FileStream compressedFileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    using (GZipStream decompressionStream = new GZipStream(compressedFileStream, CompressionMode.Decompress))
                    using (MemoryStream resultStream = new MemoryStream())
                    {
                        decompressionStream.CopyTo(resultStream);
                        decompressedData = resultStream.ToArray();
                    }
                    Console.WriteLine($"Loaded and decompressed file: {filePath}, size: {decompressedData.Length:N0} bytes");
                }
                else
                {
                    decompressedData = File.ReadAllBytes(filePath);
                    Console.WriteLine($"Loaded file: {filePath}, size: {decompressedData.Length:N0} bytes");
                }
            }

            public byte[] GetBytes(long offset, int length)
            {
                if (isDisposed)
                    throw new ObjectDisposedException(nameof(BinaryCache));

                if (offset < 0 || length <= 0)
                    throw new ArgumentException("Offset must be non-negative and length must be greater than zero.");

                if (offset + length > decompressedData.Length)
                    throw new ArgumentOutOfRangeException("The specified offset and length exceed the file size.");

                byte[] result = new byte[length];
                Array.Copy(decompressedData, offset, result, 0, length);
                return result;
            }

            public void Dispose()
            {
                if (!isDisposed)
                {
                    decompressedData = null;
                    isDisposed = true;
                    GC.SuppressFinalize(this);
                }
            }
        }

        static void ProcessFileEntry(FileEntry file, BinaryCache binaryCache, string outputDir)
        {
            try
            {
                Console.WriteLine($"Processing {file.Name} at {file.StartAddress}, length: {file.Length}");

                byte[] bytes = binaryCache.GetBytes(file.StartAddress, (int)file.Length);
                string outputPath = Path.Combine(outputDir, file.Name + ".nud");

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, bytes);

                ModelContainer modelContainer = new ModelContainer();
                modelContainer.NUD = new Nud(outputPath);

                if (modelContainer.NUD != null)
                {
                    string name = file.Name;

                    modelContainer.NUD.MergePoly();
                    Nud nud = modelContainer.NUD;
                    foreach (Nud.Mesh mesh in nud.Nodes)
                    {
                        if (!string.IsNullOrEmpty(mesh.Name))
                        {
                            name = mesh.Name;
                        }
                    }

                    string daePath = Path.Combine(outputDir, name + ".dae");
                    Collada.Save(daePath, modelContainer);
                    Console.WriteLine($"Saved {daePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {file.Name}: {ex.Message}");
            }
        }

        static void ProcessModelSection(ModelSection section, string basePath)
        {
            var binPath = Path.Combine(Path.GetDirectoryName(basePath), "bin", Path.GetFileName(section.BinPath));
            var outputDir = Path.Combine(Path.GetDirectoryName(basePath), "extracted_files", $"section_{section.SectionId}");

            // Check for .gz version first
            string gzipPath = binPath;
            string actualBinPath;

            if (File.Exists(gzipPath))
            {
                actualBinPath = gzipPath;
                Console.WriteLine($"Found compressed bin file: {gzipPath}");
            }
            else if (File.Exists(binPath))
            {
                actualBinPath = binPath;
                Console.WriteLine($"Found bin file: {binPath}");
            }
            else
            {
                Console.WriteLine($"Error: Neither {binPath} nor {gzipPath} found.");
                return;
            }

            Directory.CreateDirectory(outputDir);
            Console.WriteLine($"\nProcessing Section {section.SectionId}:");
            Console.WriteLine($"Bin: {section.BinPath}");

            // Create binary cache for this section
            using (var binaryCache = new BinaryCache(actualBinPath))
            {
                var fileListProperties = typeof(ModelSection)
                    .GetProperties()
                    .Where(p => p.PropertyType == typeof(List<FileEntry>));

                foreach (var listProperty in fileListProperties)
                {
                    var files = (List<FileEntry>)listProperty.GetValue(section);
                    if (files == null || !files.Any()) continue;

                    var listName = listProperty.Name.Replace("Files", "");
                    Console.WriteLine($"Processing {listName} Files:");

                    foreach (var file in files)
                    {
                        ProcessFileEntry(file, binaryCache, outputDir);
                    }
                }
            }
        }

        static async Task MainAsync(string[] args)
        {
            try
            {
                string luaPath = args.Length > 0 ? args[0] : throw new ArgumentException("Lua file path is required as an argument.");
                string luaContent = File.ReadAllText(luaPath);
                var parser = new LuaModelParser();

                var textures = parser.ParseTextureList(luaContent);
                foreach (var texture in textures)
                {
                    Console.WriteLine($"Texture: {texture}");
                }

                var models = parser.ParseModelList(luaContent);

                // Process sections in parallel
                var tasks = models.Select(section =>
                    Task.Run(() => ProcessModelSection(section, luaPath))
                ).ToList();

                await Task.WhenAll(tasks);

                Console.WriteLine("Done! Press any key to continue...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ReadKey();
            }
        }

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }
    }
}