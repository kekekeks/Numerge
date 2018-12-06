using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using static Numerge.Constants;

namespace Numerge
{
    class LoadedPackage
    {
        public string FileName { get; }
        public Dictionary<string, byte[]> BinaryContents { get;  } = new Dictionary<string, byte[]>();
        public LoadedNuspec Spec { get; }
        public ContentTypes ContentTypes { get; }
        private string _nuspecName;

        public LoadedPackage(string fileName, byte[] package)
        {
            FileName = fileName;
            var arch = new ZipArchive(new MemoryStream(package));
            foreach (var e in arch.Entries)
            {
                using (var s = e.Open())
                {
                    var ms = new MemoryStream();
                    s.CopyTo(ms);
                    BinaryContents.Add(e.FullName.Replace("\\", "/"), ms.ToArray());
                }
            }

            var nuspecItem = BinaryContents.First(c => c.Key.EndsWith("nuspec"));
            _nuspecName = nuspecItem.Key;
            
            Spec = new LoadedNuspec(nuspecItem.Value);
            BinaryContents.Remove(_nuspecName);
            
            ContentTypes = new ContentTypes(BinaryContents[ContentTypesFileName]);
            BinaryContents.Remove(ContentTypesFileName);
        }

        public LoadedPackage(string path) : this(Path.GetFileName(path), File.ReadAllBytes(path))
        {
            
        }

        public void ResolveBinaryDependencies(Dictionary<string, LoadedPackage> pkgs)
        {
            foreach (var grp in Spec.Dependencies)
            {
                for (var c=0; c< grp.Value.Count; c++)
                {
                    if (pkgs.TryGetValue(grp.Value[c].Id, out var bindep))
                        grp.Value[c] = new BinaryDependency(grp.Value[c], bindep);
                }
            }
        }

        public void MergeContents(INumergeLogger logger, LoadedPackage victim,
            PackageMergeConfiguration config)
        {
            var ignoredPrefixes = new[] {"lib/", "_rels/", "package/"};
            foreach (var item in victim.BinaryContents.Where(x =>
                x.Key.Contains("/") && !ignoredPrefixes.Any(p => x.Key.StartsWith(p))))
            {
                if (BinaryContents.ContainsKey(item.Key))
                    logger.Warning($"{Spec.Id}: Refusing to replace item {item.Key} with item from {victim.Spec.Id}");
                else
                {
                    BinaryContents[item.Key] = item.Value;
                }
            }

            var libs = victim.BinaryContents.Where(x => x.Key.StartsWith("lib/"))
                .Select(x => new {sp = x.Key.Split(new[] {'/'}, 3), data = x.Value})
                .Select(x => new {Tfm = x.sp[1], File = x.sp[2], Data = x.data}).GroupBy(x => x.Tfm)
                .ToDictionary(x => x.Key, x => x.ToList());

            var ourFrameworks = BinaryContents.Where(x => x.Key.StartsWith("lib/")).Select(x => x.Key.Split('/')[1])
                .Distinct().ToList();

            foreach (var foreignTfm in libs)
                if (!ourFrameworks.Contains(foreignTfm.Key))
                    throw new MergeAbortedException(
                        $"Error merging {victim.Spec.Id}: Package {Spec.Id} doesn't have target framework {foreignTfm.Key}");
            
            //TODO: Actually detect compatibility with .NET Standard
            libs.TryGetValue("netstandard2.0", out var netstandardLibs);

            foreach (var framework in ourFrameworks)
            {
                libs.TryGetValue(framework, out var frameworkLibs);
                frameworkLibs = frameworkLibs ?? netstandardLibs;
                if (frameworkLibs == null)
                {
                    if (!config.IgnoreMissingFrameworkBinaries)
                        throw new MergeAbortedException(
                            $"Unable to merge {victim.Spec.Id} to {Spec.Id}: {victim.Spec.Id} doesn't support {framework} or netstandard2.0");
                }
                else

                    foreach (var lib in frameworkLibs)
                    {
                        var targetPath = $"lib/{framework}/{lib.File}";
                        if (BinaryContents.ContainsKey(targetPath))
                            logger.Warning(
                                $"{Spec.Id}: Refusing to replace item {targetPath} with item from {victim.Spec.Id}");
                        else
                            BinaryContents[targetPath] = lib.Data;
                    }
            }

           

            if (!config.DoNotMergeDependencies)
            {
                //TODO: Actually detect compatibility with .NET Standard
                if (!victim.Spec.Dependencies.TryGetValue(".NETStandard2.0", out var netstandardDeps))
                    victim.Spec.Dependencies.TryGetValue("netstandard2.0", out netstandardDeps);
                
                var handledDepFrameworks = new HashSet<string>();
                foreach (var group in victim.Spec.Dependencies)
                {
                    if (!Spec.Dependencies.TryGetValue(group.Key, out var ourGroup))
                        Spec.Dependencies[group.Key] = ourGroup = new List<IDependency>();
                    ourGroup.AddRange(group.Value);
                    handledDepFrameworks.Add(group.Key);
                }

                foreach (var ourGroup in Spec.Dependencies)
                {
                    // Merge deps
                    if (!handledDepFrameworks.Contains(ourGroup.Key))
                    {
                        if (netstandardDeps == null)
                        {
                            if (!config.IgnoreMissingFrameworkDependencies)
                                throw new MergeAbortedException(
                                    $"Unable to merge dependencies from {victim.Spec.Id} to {Spec.Id}: {victim.Spec.Id} doesn't have deps for {ourGroup.Key} or netstandard2.0");
                        }
                        else
                        {
                            ourGroup.Value.AddRange(netstandardDeps);
                        }
                    }

                    // Remove merged dep
                    ourGroup.Value.Where(d => d.Id == victim.Spec.Id).ToList().ForEach(i => ourGroup.Value.Remove(i));
                }
            }

            foreach (var ct in victim.ContentTypes)
                if (!ContentTypes.ContainsKey(ct.Key))
                    ContentTypes[ct.Key] = ct.Value;

        }
        
        public void ReplaceDeps(string from, LoadedPackage to)
        {
            foreach (var group in Spec.Dependencies)
            {
                for (var c = 0; c < group.Value.Count; c++)
                {
                    if (group.Value[c].Id == from)
                        group.Value[c] = new BinaryDependency(group.Value[c], to);
                }
            }
        }

        public void Save(Stream stream)
        {
            var contents = BinaryContents.ToDictionary(x => x.Key, x => x.Value);
            contents[_nuspecName] = Spec.Serialize();
            contents[ContentTypesFileName] = ContentTypes.Serialize();
            using (var arch = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var c in contents)
                {
                    var e = arch.CreateEntry(c.Key, CompressionLevel.Optimal);
                    using (var es = e.Open())
                        es.Write(c.Value, 0, c.Value.Length);
                }
            }
        }

        public void SaveToDirectory(string dirPath)
        {
            Directory.CreateDirectory(dirPath);
            var path = Path.Combine(dirPath, FileName);
            using (var s = File.Create(path))
                Save(s);
        }
        
    }


    class LoadedNuspec
    {
        private readonly byte[] _data;
        public string Id { get; set; }
        public string Version { get; set; }
        private string _xmlns;
        
        public XName NugetName(string name) => XName.Get(name, _xmlns);
        
        public Dictionary<string, List<IDependency>> Dependencies { get; } = new Dictionary<string, List<IDependency>>();

        public LoadedNuspec(byte[] data)
        {
            
            _data = data;
            var doc = XDocument.Load(new MemoryStream(data));
            _xmlns = doc.Root.Name.Namespace.ToString();
            
            var deps = doc.Root.Descendants(NugetName("dependencies")).First();
            foreach (var group in deps.Elements(NugetName("group")))
            {
                var tfm = group.Attribute("targetFramework").Value;
                var groupList = Dependencies[tfm] = new List<IDependency>();
                foreach (var dep in group.Elements())
                    groupList.Add(new ExternalDependency(dep));
            }

            var metadata = doc.Root.Element(NugetName("metadata"));
            Id = metadata.Element(NugetName("id")).Value;
            Version = metadata.Element(NugetName("version")).Value;
        }

        public byte[] Serialize()
        {
            RemoveDuplicates();
            RemoveSelfDependency();
            var doc = XDocument.Load(new MemoryStream(_data));
            var deps = doc.Root.Descendants(NugetName("dependencies")).First();
            deps.RemoveAll();
            foreach (var group in Dependencies)
            {
                var el = new XElement(NugetName("group"));
                el.SetAttributeValue("targetFramework", group.Key);
                deps.Add(el);
                foreach (var dep in group.Value)
                    el.Add(dep.Serialize(_xmlns));
            }
            var ms = new MemoryStream();
            doc.Save(ms, SaveOptions.OmitDuplicateNamespaces);
            return ms.ToArray();
        }

        public void RemoveSelfDependency()
        {
            foreach(var group in Dependencies)
                foreach(var d in group.Value.ToList())
                    if (d.Id == Id)
                        group.Value.Remove(d);
        }

        public void RemoveDuplicates()
        {
            foreach (var group in Dependencies)
            {
                var hs = new HashSet<string>();
                foreach(var dep in group.Value.ToList())
                    if (!hs.Add(dep.Id))
                        group.Value.Remove(dep);
            }
        }
    }

    interface IDependency
    {
        string Id { get; }
        string ExcludeAssets { get; }
        XElement Serialize(string xmlna);
    }

    class ExternalDependency : IDependency
    {
        private readonly XElement _el;

        public ExternalDependency(XElement el)
        {
            _el = el;
            Id = el.Attribute("id").Value;
            ExcludeAssets = el.Attribute("exclude")?.Value;
        }

        public string Id { get; }
        public string ExcludeAssets { get; }
        public XElement Serialize(string xmlns)
        {
            var rv = XElement.Load(_el.CreateReader());
            foreach (var e in rv.DescendantsAndSelf())
            {
                e.Name = XName.Get(rv.Name.LocalName, xmlns);
                foreach (var a in e.Attributes().ToList())
                {
                    a.Remove();
                    e.SetAttributeValue(XName.Get(a.Name.LocalName), a.Value);
                }
            }

            return rv;
        }
    }

    class BinaryDependency : IDependency
    {
        public LoadedPackage Package { get; }
        public string Id => Package.Spec.Id;
        public string ExcludeAssets { get; set; }

        public BinaryDependency(IDependency original, LoadedPackage package)
        {
            Package = package;
            ExcludeAssets = original.ExcludeAssets;
        }
        
        public XElement Serialize(string xmlns)
        {
            var rv = new XElement(XName.Get("dependency", xmlns));
            rv.SetAttributeValue("id", Id);
            rv.SetAttributeValue("version", Package.Spec.Version);
            rv.SetAttributeValue("exclude", ExcludeAssets);
            return rv;
        }
    }

    class ContentTypes : Dictionary<string, string>
    {
        public ContentTypes(byte[] data)
        {
            var doc = XDocument.Load(new MemoryStream(data));
            foreach (var el in doc.Root.Elements())
                this[el.Attribute("Extension").Value] =
                    el.Attribute("ContentType").Value;
        }

        public byte[] Serialize()
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", ""),
                new XElement(ContentTypesName("Types")));
            foreach (var kp in this)
            {
                var el = new XElement(ContentTypesName("Default"));
                el.SetAttributeValue("Extension", kp.Key);
                el.SetAttributeValue("ContentType", kp.Value);
            }
            var ms = new MemoryStream();
            doc.Save(ms);
            return ms.ToArray();
        }
    }
}