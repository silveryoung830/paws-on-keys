using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace PawsOnKeys
{
    public sealed class CatLook
    {
        public string Name { get; set; }
        public string Fur { get; set; }
        public string Eyes { get; set; }
        public string Marking { get; set; }
        public string Pattern { get; set; }
        public bool LeftSock { get; set; }
        public bool RightSock { get; set; }
        public bool LeftCheek { get; set; }
        public bool RightCheek { get; set; }
        public double Build { get; set; }

        public CatLook()
        {
            Name = "小黑";
            Fur = "#232127";
            Eyes = "#DEF36B";
            Marking = "#F5EBDD";
            Pattern = "solid";
            Build = 1.0;
        }

        public CatLook Copy() { return (CatLook)MemberwiseClone(); }

        public void Normalize()
        {
            Name = string.IsNullOrWhiteSpace(Name) ? "小黑" : Name.Trim();
            if (Name.Length > 40) Name = Name.Substring(0, 40);
            Fur = SafeColor(Fur, "#232127");
            Eyes = SafeColor(Eyes, "#DEF36B");
            Marking = SafeColor(Marking, "#F5EBDD");
            if (Pattern != "tabby" && Pattern != "tuxedo" && Pattern != "calico") Pattern = "solid";
            if (double.IsNaN(Build) || double.IsInfinity(Build)) Build = 1.0;
            Build = Math.Max(0.8, Math.Min(1.25, Build));
        }

        private static string SafeColor(string value, string fallback)
        {
            if (value == null || value.Length != 7 || value[0] != '#') return fallback;
            for (int i = 1; i < value.Length; i++) if (!Uri.IsHexDigit(value[i])) return fallback;
            return value.ToUpperInvariant();
        }
    }

    public sealed class PetSettings
    {
        public CatLook Cat { get; set; }
        public bool Configured { get; set; }
        public bool ShowKeys { get; set; }
        public bool Paused { get; set; }
        public string Corner { get; set; }
        public string ScreenName { get; set; }
        public int Width { get; set; }

        public PetSettings()
        {
            Cat = new CatLook();
            ShowKeys = true;
            Corner = "BR";
            Width = 310;
        }

        public static string DefaultPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PawsOnKeys", "pet.xml"); }
        }

        public static PetSettings Load(string file)
        {
            if (!File.Exists(file)) return new PetSettings();
            try
            {
                PetSettings settings;
                using (XmlReader reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 50000 }))
                    settings = (PetSettings)new XmlSerializer(typeof(PetSettings)).Deserialize(reader);
                if (settings == null) return new PetSettings();
                if (settings.Cat == null) settings.Cat = new CatLook();
                settings.Cat.Normalize();
                if (settings.Corner != "BL" && settings.Corner != "TL" && settings.Corner != "TR") settings.Corner = "BR";
                settings.Width = Math.Max(220, Math.Min(480, settings.Width));
                return settings;
            }
            catch (IOException) { return new PetSettings(); }
            catch (UnauthorizedAccessException) { return new PetSettings(); }
            catch (InvalidOperationException) { return new PetSettings(); }
            catch (XmlException) { return new PetSettings(); }
        }

        public void Save(string file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
            string temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    new XmlSerializer(typeof(PetSettings)).Serialize(stream, this);
                if (File.Exists(file)) File.Replace(temp, file, null);
                else File.Move(temp, file);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}