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
using System;
using System.Collections.Generic;

namespace WanganTools
{
    internal class Program
    {
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

                    // Parse LONG files
                    ParseFileEntries(sectionData.Table, "LONG", section.LongFiles);

                    // Parse NEAR files
                    ParseFileEntries(sectionData.Table, "NEAR", section.NearFiles);

                    // Parse LODM files
                    ParseFileEntries(sectionData.Table, "LODM", section.LodmFiles);

                    // Parse ROAD files
                    ParseFileEntries(sectionData.Table, "ROAD", section.RoadFiles);

                    // Parse ONRD files
                    ParseFileEntries(sectionData.Table, "ONRD", section.OnrdFiles);

                    // Parse BACK files
                    ParseFileEntries(sectionData.Table, "BACK", section.BackFiles);

                    // Parse CAST files
                    ParseFileEntries(sectionData.Table, "CAST", section.CastFiles);

                    // Parse REFC files
                    ParseFileEntries(sectionData.Table, "REFC", section.RefcFiles);

                    // Parse REFR files
                    ParseFileEntries(sectionData.Table, "REFR", section.RefrFiles);

                    // Parse RFBG files
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

        static byte[] ExtractBytes(string filePath, long offset, int length)
        {
            // Validate input
            if (offset < 0 || length <= 0)
            {
                throw new ArgumentException("Offset must be non-negative and length must be greater than zero.");
            }

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                // Ensure the offset and length are within file bounds
                if (offset + length > fs.Length)
                {
                    throw new ArgumentOutOfRangeException("The specified offset and length exceed the file size.");
                }

                // Seek to the desired offset
                fs.Seek(offset, SeekOrigin.Begin);

                // Read the specified number of bytes
                byte[] buffer = new byte[length];
                int bytesRead = fs.Read(buffer, 0, length);

                // Ensure we read the expected number of bytes
                if (bytesRead != length)
                {
                    throw new IOException("Failed to read the specified number of bytes.");
                }

                return buffer;
            }
        }

        static void Main(string[] args)
        {
            //Read .lua file
            string luaPath = "D:\\wangan\\LOADLIST_A_TOKYO_NGT_NML_WANGAN.lua";
            string luaContent = File.ReadAllText(luaPath);
            var parser = new LuaModelParser();

            // Parse textures
            var textures = parser.ParseTextureList(luaContent);
            foreach (var texture in textures)
            {
                Console.WriteLine($"Texture: {texture}");
            }

            // Parse models
            var models = parser.ParseModelList(luaContent);
            foreach (var section in models)
            {
                Console.WriteLine($"\nSection {section.SectionId}:");
                Console.WriteLine($"Bin: {section.BinPath}");

                var binPath = Path.GetDirectoryName(luaPath) + "\\bin\\" + Path.GetFileNameWithoutExtension(section.BinPath);
                var fileListProperties = typeof(ModelSection)
                    .GetProperties()
                    .Where(p => p.PropertyType == typeof(List<FileEntry>));

                // Process each list of files
                foreach (var listProperty in fileListProperties)
                {
                    var files = (List<FileEntry>)listProperty.GetValue(section);

                    // Skip if the list is empty
                    if (files == null || !files.Any()) continue;

                    var listName = listProperty.Name.Replace("Files", ""); // Remove "Files" suffix for display

                    Console.WriteLine($"{listName} Files:");
                    foreach (var file in files)
                    {
                        Console.WriteLine($"  {file.Name} at {file.StartAddress}, length: {file.Length}");
                        var bytes = ExtractBytes(binPath, file.StartAddress, (int)file.Length);
                        var outputDir = Path.GetDirectoryName(luaPath) + "\\extracted_files\\";
                        if (!Directory.Exists(outputDir))
                        {
                            Directory.CreateDirectory(outputDir);
                        }
                        File.WriteAllBytes(outputDir + file.Name + ".nud", bytes);

                        ModelContainer modelContainer = new ModelContainer();
                        modelContainer.NUD = new Nud(outputDir + file.Name + ".nud");

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

                            Collada.Save(Path.Combine(outputDir, name + ".dae"), modelContainer);
                            Console.WriteLine("Saved " + Path.Combine(outputDir, name + ".dae"));
                        }
                    }
                }
            }
            Console.WriteLine("Done! Press any key to continue...");
            Console.ReadKey();
        }
    }
}
