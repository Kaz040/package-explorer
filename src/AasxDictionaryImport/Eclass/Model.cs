﻿/*
Copyright (c) 2020 SICK AG <info@sick.de>

This source code is licensed under the Apache License 2.0 (see LICENSE.txt).

This source code may use other Open Source software components (see LICENSE.txt).
*/

#nullable enable

using AasxDictionaryImport.Model;
using AasxPackageLogic;
using AdminShellNS;
using Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Linq;
using Aas = AasCore.Aas3_0;


namespace AasxDictionaryImport.Eclass
{
    /// <summary>
    /// Data provider for ECLASS Basic data.  The data is read from XML files using the OntoML scheme, see
    /// <see cref="Context"/> for more information.  The complete ECLASS exports use one file per language and domain.
    /// In this implementation, a data source is always backed by an English export file.  If present, export files in
    /// other languages and unit files are loaded additionally.
    /// <para>
    /// This data provider assumes that the ECLASS XML files follow this naming convention:
    /// <list type="bullet">
    /// <item><description><code>&lt;Prefix&gt;_&lt;Version&gt;_&lt;Lang&gt;_*.xml</code> for class
    /// exports, and</description></item>
    /// <item><description><code>&lt;Prefix&gt;_UnitsML_*.xml</code> for unit data,</description></item>
    /// </list>
    /// where <code>&lt;Prefix&gt;</code> is a constant prefix per ECLASS release, <code>&lt;Version&gt;</code> is
    /// either <code>BASIC</code> or <code>ADVANCED</code> and <code>&lt;Lang&gt;</code> is an uppercase two-character
    /// language code (e. g. <code>EN</code> for the English export).
    /// </para>
    /// </summary>
    /// <seealso href="https://wiki.eclass.eu/wiki/ISO_13584-32_ontoML"/>
    public class DataProvider : Model.DataProviderBase
    {
        /// <inheritdoc/>
        public override string Name => "ECLASS";

        /// <inheritdoc/>
        public override bool IsFetchSupported => true;

        /// <inheritdoc/>
        public override string FetchPrompt => "IRDI / Code";

        /// <summary>
        /// Checks whether the given path contains valid ECLASS data that can be read by this data provider.  If this
        /// method returns true, the data source at the given path can be opened using the <see cref="OpenPath"/>
        /// method.
        /// <para>
        /// This function performs the following checks:
        /// <list>
        /// <item><description>The path must be a valid XML file.</description></item>
        /// <item><description>The XML file must be an ECLASS dictionary.</description></item>
        /// <item><description>The language of the XML file must be English (see class comment).</description>
        /// </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="path">The path of the data to check</param>
        /// <returns>true if the path is a valid data source for this provider</returns>
        public override bool IsValidPath(string path)
        {
            if (!File.Exists(path))
                return false;

            try
            {
                // We only want to look at the first few elements.  Therefore we use XmlReader instead of XDocument.
                using var reader = XmlReader.Create(path);

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        // Verify that the root element is eclass_dictionary and find the header element for different
                        // schema versions
                        if (reader.IsStartElement("eclass_dictionary", Namespaces.Dic30.NamespaceName))
                        {
                            if (!reader.ReadToDescendant("header", Namespaces.Hea30.NamespaceName))
                                return false;
                        }
                        else if (reader.IsStartElement("eclass_dictionary", Namespaces.Dic20.NamespaceName))
                        {
                            if (!reader.ReadToDescendant("header", Namespaces.Hea20.NamespaceName))
                                return false;
                        }
                        else
                        {
                            return false;
                        }

                        // The content language must be English, see class comment
                        if (!reader.ReadToDescendant("content_language"))
                            return false;
                        var lang = reader.GetAttribute("language_ref");
                        return lang != null && lang.StartsWith("0112-1#LG-EN#");
                    }
                }

                return false;
            }
            catch (XmlException)
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public override Model.IDataSource Fetch(string query)
        {
            return new DataSource(this, FetchXmlFile(query), Model.DataSourceType.Online);
        }

        /// <summary>
        /// Fetch the XML data for the ECLASS element with the given IRDI using the ECLASS web service, write it to a
        /// file and return the path to the file.  The file name of the created file should identify the fetched
        /// element as it is shown as the name of the data source in the user interface.
        /// </summary>
        /// <param name="irdi">The IRDI of the ECLASS element to fetch from the web service</param>
        /// <returns>The path of the XML file containing the data for the ECLASS element with the given IRDI</returns>
        /// <exception cref="Model.ImportException">If the element could not be fetched from the web API</exception>
        private string FetchXmlFile(string irdi)
        {
            X509Store store = new X509Store("MY", StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            X509Certificate2Collection collection = store.Certificates;
            X509Certificate2Collection fcollection = collection.Find(
                X509FindType.FindByTimeValid, DateTime.Now, false);

            Boolean certFound = false;
            X509Certificate2Collection fcollection2 = new X509Certificate2Collection();
            foreach (X509Certificate2 fc in fcollection)
            {
                if (fc.Issuer.ToLower().Contains("eclass-cdp.com"))
                {
                    certFound = true;
                    fcollection2.Add(fc);
                }
            }

            X509Certificate2 cert = new X509Certificate2();
            if (certFound)
            {
                certFound = false;
                X509Certificate2Collection scollection = X509Certificate2UI.SelectFromCollection(fcollection2,
                    "Test Certificate Select",
                    "Select an ECLASS client certificate which you already imported into your certificate store",
                    X509SelectionFlag.SingleSelection);
                if (scollection.Count != 0)
                {
                    certFound = true;
                    cert = scollection[0];
                }
            }

            if (!certFound)
            {
                throw new ImportException($"No valid ECLASS certificate selected");
            }

            HttpClientHandler clientHandler = new HttpClientHandler();
            clientHandler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
            clientHandler.ClientCertificates.Add(cert);
            clientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;

            var url = "https://eclass-cdp.com/xmlapi/v1/classes/" + Uri.EscapeDataString(irdi);

            HttpResponseMessage response;
            try
            {
                HttpClient client = new HttpClient(clientHandler);
                response = client.GetAsync(url).Result;
            }
            catch (HttpRequestException ex)
            {
                throw new ImportException("Failed to perform ECLASS query", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ImportException($"ECLASS query failed with status code: {response.ReasonPhrase}");
            }

            var tempDir = Path.Combine(Path.Combine(Path.GetTempPath(), $"aasx.import"), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, irdi + ".xml");

            using var fs = new FileStream(tempFile, FileMode.CreateNew);
            response.Content.CopyToAsync(fs).Wait();

            return tempFile;
        }

        /// <inheritdoc/>
        protected override IEnumerable<string> GetDefaultPaths(string dir)
        {
            var searchDirectory = System.IO.Path.Combine(dir, "eclass");
            if (!System.IO.Directory.Exists(searchDirectory))
                return new List<string>();
            return System.IO.Directory.GetFiles(searchDirectory);
        }

        /// <inheritdoc/>
        public override Model.IDataSource OpenPath(string path, Model.DataSourceType type = DataSourceType.Custom)
            => new DataSource(this, path, type);
    }

    /// <summary>
    /// Data source for ECLASS data.  The ECLASS data is read from XML files, and a data source represents one English
    /// ECLASS XML file, optionally with additional XML files in other languages.  For more information on the XML
    /// parser, see the <see cref="Context"/> and <see cref="Element"/> classes.
    /// </summary>
    public class DataSource : Model.FileSystemDataSource
    {
        /// <summary>
        /// Creates a new DataSource object with the given data.
        /// </summary>
        /// <param name="dataProvider">The data provider for this data source</param>
        /// <param name="path">The path of the ECLASS XML file</param>
        /// <param name="type">The type of the data source</param>
        public DataSource(Model.IDataProvider dataProvider, string path, Model.DataSourceType type)
           : base(dataProvider, path, type)
        {
        }

        /// <inheritdoc/>
        public override Model.IDataContext Load()
        {
            try
            {
                var xml = XDocument.Load(Path);
                var additionalXml = FindAdditionalFiles().Select(f => XDocument.Load(f)).ToList();
                var units = FindUnits();

                if (units == null)
                    Log.Singleton.Info("Could not find units for ECLASS import.");

                return new Context(this, xml, additionalXml, units);
            }
            catch (XmlException e)
            {
                throw new Model.ImportException($"Could not load the XML document at '{Path}'", e);
            }
        }

        private IEnumerable<string> FindAdditionalFiles()
        {
            var dir = System.IO.Path.GetDirectoryName(Path);
            var searchPattern = System.IO.Path.GetFileName(Path).Replace("_EN_", "_??_");
            return Directory.GetFiles(dir, searchPattern).Where(path => path != Path);
        }

        private XDocument? FindUnits()
        {
            var dir = System.IO.Path.GetDirectoryName(Path);
            var dictName = System.IO.Path.GetFileName(Path);
            var i = dictName.IndexOf("BASIC", System.StringComparison.Ordinal);
            if (i < 0)
                i = dictName.IndexOf("ADVANCED", System.StringComparison.Ordinal);
            if (i < 0)
                return null;

            var pattern = dictName.Substring(0, i) + "UnitsML*.xml";
            var files = Directory.GetFiles(dir, pattern);
            if (files.Length != 1)
                return null;

            return XDocument.Load(files[0]);
        }
    }

    /// <summary>
    /// Data context for ECLASS data.  The context loads the classes and properties from an XML document.  It
    /// removes all deprecated elements because they are no longer part of the ECLASS release.  It also computes a
    /// mapping from classification classes to application classes.  For more information on the class types, see
    /// <see cref="Class"/>.
    /// <para>
    /// The main class structure is loaded from the English-language XML export.  XML files for other languages and an
    /// XML file with information about the units can be loaded additionally.
    /// </para>
    /// </summary>
    public class Context : Model.IDataContext
    {
        private readonly IDictionary<string, Element> _elements = new Dictionary<string, Element>();

        private readonly ICollection<string> _deprecatedElements = new List<string>();

        /// <summary>
        /// The data source this data has been read from.
        /// </summary>
        public DataSource DataSource { get; }

        /// <summary>
        /// The top-level classes in this data context.
        /// </summary>
        public ICollection<Class> Classes { get; }

        /// <inheritdoc/>
        public ICollection<Model.UnknownReference> UnknownReferences { get; } = new List<Model.UnknownReference>();

        /// <summary>
        /// Creates a new ECLASS Context and loads the data from the given XML document.
        /// </summary>
        /// <param name="dataSource">The data source for this context</param>
        /// <param name="document">The XML document to read the data from</param>
        /// <param name="additionalDocuments">Additional XML documents with translations for the data stored in
        /// <paramref name="document"/></param>
        /// <param name="units">A UnitsML XML document with information about the units used in the ECLASS data, if
        /// available</param>
        public Context(DataSource dataSource, XDocument document, ICollection<XDocument> additionalDocuments,
            XDocument? units)
        {
            DataSource = dataSource;

            var classes = document.Descendants(Namespaces.OntoML + "class").Select(e => new Class(this, e)).ToList();
            AddElements(classes);
            Classes = classes.Where(c => !c.IsDeprecated).ToList();
            AssignApplicationClasses();
            AssignAspects(document.Descendants(Namespaces.OntoML + "a_posteriori_semantic_relationship"));

            var properties = document
                .Descendants(Namespaces.OntoML + "property")
                .Select(e => new Property(this, e))
                .ToList();
            AddElements(properties);
            if (units != null)
                AssignUnits(properties, units);

            foreach (var additionalDocument in additionalDocuments)
                LoadTranslations(additionalDocument);
        }

        private void LoadTranslations(XDocument document)
        {
            LoadTranslations(document.Descendants(Namespaces.OntoML + "class"));
            LoadTranslations(document.Descendants(Namespaces.OntoML + "property"));
        }

        private void LoadTranslations(IEnumerable<XElement> elements)
        {
            foreach (var element in elements)
                LoadTranslations(element);
        }

        private void LoadTranslations(XElement element)
        {
            var idAttr = element.Attribute("id");
            if (idAttr == null)
                return;

            if (_elements.TryGetValue(idAttr.Value, out Element oldElement))
                oldElement.AddTranslation(element);
        }

        private void AssignUnits(ICollection<Property> properties, XDocument document)
        {
            var units = LoadUnits(document);
            foreach (var property in properties)
            {
                var unitIrdi = property.UnitIrdi;
                if (unitIrdi.Length > 0 && units.TryGetValue(unitIrdi, out string unit))
                    property.Unit = unit;
            }
        }

        private IDictionary<string, string> LoadUnits(XDocument document)
        {
            var units = new Dictionary<string, string>();
            foreach (var element in document.Descendants(Namespaces.UnitsML + "Unit"))
            {
                var data = element
                    .Elements(Namespaces.UnitsML + "CodeListValue")
                    .ToDictionary(e => e.Attributes("codeListName").FirstValue(),
                        e => e.Attributes("unitCodeValue").FirstValue());
                if (data.TryGetValue("IRDI", out string irdi) && data.TryGetValue("SI code", out string siCode))
                {
                    if (!units.ContainsKey(irdi))
                        // TODO (krahlro-sick, 2021-02-03): HTML-decode SI code
                        units.Add(irdi, siCode);
                }
            }
            return units;
        }

        private void AssignApplicationClasses()
        {
            foreach (var cls in Classes)
            {
                foreach (var cclsId in cls.ClassificationClassIds)
                {
                    var ccls = GetElement<Class>(cclsId);
                    if (ccls != null)
                        ccls.ApplicationClasses.Add(cls);
                }
            }
        }

        private void AssignAspects(IEnumerable<XElement> aspectRelations)
        {
            foreach (var aspectRelation in aspectRelations)
            {
                var applicationClassIrdi = aspectRelation.Elements("item").Attributes("class_ref").FirstValue();
                var aspectIrdi = aspectRelation.Elements("model").Attributes("class_ref").FirstValue();
                if (applicationClassIrdi.Length == 0 || aspectIrdi.Length == 0)
                    continue;

                var applicationClass = GetElement<Class>(applicationClassIrdi);
                var aspect = GetElement<Class>(aspectIrdi);
                if (applicationClass != null && aspect != null)
                    applicationClass.Aspects.Add(aspect);
            }
        }

        private void AddElements<T>(IEnumerable<T> elements) where T : Element
        {
            foreach (var element in elements)
            {
                if (element.IsDeprecated)
                {
                    _deprecatedElements.Add(element.Id);
                }
                else
                {
                    if (!_elements.ContainsKey(element.Id))
                        _elements.Add(element.Id, element);
                }
            }
        }

        /// <inheritdoc/>
        public ICollection<Model.IElement> LoadSubmodels() => Classes.Cast<Model.IElement>().ToList();

        /// <inheritdoc/>
        public ICollection<Model.IElement> LoadSubmodelElements() => _elements.Values.Cast<Model.IElement>().ToList();

        public Property? GetProperty(Class parent, string id)
        {
            var property = GetElement<Property>(id);
            if (property != null)
                return new Property(property, parent);
            return null;
        }

        public T? GetElement<T>(string id) where T : Element
        {
            if (_elements.TryGetValue(id, out Element e))
                if (e is T t)
                    return t;

            // We don't have to record deprecated elements as unknown references because we don't add them to _elements
            // in the first place
            if (!_deprecatedElements.Contains(id))
                UnknownReferences.Add(Model.UnknownReference.Create<Property>(id));
            return null;
        }
    }

    /// <summary>
    /// An element in the ECLASS data set, backed by an XML element from the exported XML document.  Currently, we
    /// support <see cref="Class"/> and <see cref="Property"/> elements.
    /// </summary>
    public abstract class Element : Model.LazyElementBase
    {
        protected XElement XElement;

        private ICollection<XElement> _translations = new List<XElement>();

        /// <summary>
        /// The current data context.
        /// </summary>
        public Context Context { get; }

        /// <inheritdoc/>
        public override string Id => XElement.Attributes("id").FirstValue();

        /// <inheritdoc/>
        public override string Name => PreferredName.GetDefault();

        public string Revision => XElement.Elements("revision").FirstValue();

        public MultiString PreferredName => GetMultiString("preferred_name", "label");

        public MultiString Definition => GetMultiString("definition", "text");

        public string HierarchicalPosition => XElement.Elements("hierarchical_position").FirstValue();

        public ICollection<string> Hierarchy
        {
            get
            {
                if (Parent == null || !(Parent is Element element))
                    return new[] { Id }.ToList();
                var hierarchy = element.Hierarchy;
                hierarchy.Add(Id);
                return hierarchy;
            }
        }

        /// <summary>
        /// Whether this element is deprecated.  If this property is true, this element
        /// should be ignored as it is no longer part of the official ECLASS release.
        /// </summary>
        public bool IsDeprecated => XElement.Elements("is_deprecated").FirstValue() == "true";

        protected Element(Context context, XElement element, Model.IElement? parent = null)
            : base(context.DataSource, parent)
        {
            XElement = element;
            Context = context;
        }

        protected Element(Element element, Model.IElement? parent = null)
            : this(element.Context, element.XElement, parent)
        {
            _translations = element._translations;
        }

        /// <summary>
        /// Adds the given XML element as a source for translations for the main XML element.
        /// </summary>
        /// <param name="element">An element to read additional translations from</param>
        public void AddTranslation(XElement element)
        {
            _translations.Add(element);
        }

        protected virtual Iec61360Data GetIec61360Data()
        {
            return new Iec61360Data(Id)
            {
                Definition = Definition,
                PreferredName = PreferredName,
            };
        }

        protected MultiString GetMultiString(string name, string childElement)
        {
            var ms = new MultiString();
            AddStrings(ms, XElement, name, childElement);
            foreach (var translation in _translations)
                AddStrings(ms, translation, name, childElement);
            return ms;
        }

        private static void AddStrings(MultiString ms, XElement element, string name, string childElement)
        {
            foreach (var label in element.Elements(name).Elements(childElement))
            {
                var lang = label.Attribute("language_code");
                if (lang != null && lang.Value.Length > 0)
                    ms.Add(lang.Value, label.Value);
            }
        }

        /// <inheritdoc/>
        protected override bool Match(string query)
        {
            // The ECLASS hierarchical position is typically displayed as groups of two digits, separated by a hyphen,
            // but is stored without hyphens in the XML file.  Therefore we remove the hyphens from the query string.
            // To keep the hierarchical character of the field, we only search at the beginning of the string.
            return base.Match(query) || HierarchicalPosition.StartsWith(query.Replace("-", ""));
        }

        /// <inheritdoc/>
        public override Dictionary<string, string> GetDetails()
        {
            return new Dictionary<string, string>
            {
                { "ID", Id },
                { "Revision", Revision },
                { "Preferred Name", PreferredName.GetDefault() },
                { "Definition", Definition.GetDefault() },
                { "Hierarchical Position", HierarchicalPosition },
            };
        }
    }

    /// <summary>
    /// An ECLASS class.  ECLASS has two types of classes:  application classes and classification classes.
    /// Classification classes are part of the classification hierarchy while an application class defines the
    /// properties for a classification class.  Unfortunately, the classification classes do not store references to
    /// their application classes.  Therefore we manually compute this mapping in <see cref="Context"/> when loading
    /// the data from the XML file.
    /// <para>
    /// In ECLASS Basic, there is a 1:1 relation between classification and application classes.  In ECLASS Advanced,
    /// there may be several application classes per classification class (typically one for the basic attributes and
    /// one for the advanced attributes).  As this implementation only supports ECLASS Basic, we assume that there is
    /// at most one application class per classification class and just transfer the application class's properties to
    /// the classification class.
    /// </para>
    /// </summary>
    public class Class : Element
    {
        /// <summary>
        /// The IDs of the classification classes that this application class describes, or an empty list if this is a
        /// classification class.
        /// </summary>
        public ICollection<string> ClassificationClassIds
            => XElement.Elements("is_case_of").Elements("class").Attributes("class_ref").Values().ToList();

        /// <summary>
        /// The application classes for this classification class, or an empty list if this is an application class.
        /// </summary>
        public ICollection<Class> ApplicationClasses { get; } = new List<Class>();

        /// <summary>
        /// The aspects for this application class, or an empty list if this is an classification class.
        /// </summary>
        public ICollection<Class> Aspects { get; } = new List<Class>();

        /// <inheritdoc/>
        public override string DisplayName
        {
            get
            {
                var name = PreferredName.GetDefault();
                if (Id.Contains("BASIC"))
                {
                    return $"{name} (ECLASS Basic)";
                }
                else
                {
                    return name;
                }
            }
        }

        /// <summary>
        /// Creates a new Class object within the given context, backed by the given XML element.
        /// </summary>
        /// <param name="context">The context for this element</param>
        /// <param name="element">The XML element with the data for this element</param>
        public Class(Context context, XElement element) : base(context, element)
        {
            if (IsAspect())
                IsSelected = false;
        }

        private bool IsAspect()
        {
            return XElement.Attributes(Namespaces.Xsi + "type").FirstValue() == "ontoml:FUNCTIONAL_MODEL_CLASS_Type";
        }

        /// <inheritdoc/>
        public override bool ImportSubmodelInto(Aas.IEnvironment env,
            Aas.IAssetAdministrationShell adminShell)
        {
            if (!IsSelected)
                return false;

            if (ApplicationClasses.Count > 0)
            {
                return ApplicationClasses.Count(ac => ac.ImportSubmodelInto(env, adminShell)) > 0;
            }
            else
            {
                var submodel = Iec61360Utils.CreateSubmodel(env, adminShell, GetIec61360Data());
                foreach (var child in Children)
                {
                    if (child is Class cls && cls.IsAspect())
                    {
                        cls.ImportSubmodelInto(env, adminShell);
                    }
                    else
                    {
                        child.ImportSubmodelElementsInto(env, submodel);
                    }
                }
                return true;
            }
        }

        /// <inheritdoc/>
        public override bool ImportSubmodelElementsInto(Aas.IEnvironment env,
            Aas.IReferable parent)
        {
            if (!IsSelected)
                return false;

            var collection = Iec61360Utils.CreateCollection(env, GetIec61360Data());
            foreach (var child in Children)
                child.ImportSubmodelElementsInto(env, collection);
            parent.Add(collection);
            return true;
        }

        /// <inheritdoc/>
        protected override ICollection<Model.IElement> LoadChildren()
        {
            if (ApplicationClasses.Count == 1)
                return ApplicationClasses.First().LoadChildren(this);
            else if (ApplicationClasses.Count > 1)
                return ApplicationClasses.Cast<Model.IElement>().ToList();

            var children = new List<Model.IElement>();
            children.AddRange(Aspects);
            children.AddRange(LoadChildren(this));
            return children;
        }

        private ICollection<Model.IElement> LoadChildren(Class parent)
        {
            // We have to force the conversion to nun-nullable types using the ! operator because Where(e => e != null)
            // still returns a nullable type, see https://github.com/dotnet/roslyn/issues/37468
            return XElement
                .Elements("described_by")
                .Elements("property")
                .Attributes("property_ref")
                .Select(a => Context.GetProperty(parent, a.Value))
                .Where(p => p != null)
                .Select(p => p!.ResolveReference())
                .Where(e => e != null)
                .Select(e => e!)
                .ToList();
        }
    }

    /// <summary>
    /// An ECLASS property.  ECLASS properties are assigned to application classes, see <see cref="Class"/>.
    /// </summary>
    public class Property : Element
    {
        public MultiString ShortName => GetMultiString("short_name", "label");

        public string Type => XElement.Elements("domain").Attributes(Namespaces.Xsi + "type").FirstValue();

        public string Unit { get; set; } = string.Empty;

        public string UnitIrdi => XElement.Elements("domain").Elements("unit").Attributes("unit_ref").FirstValue();

        public string ClassReferenceIrdi
            => XElement.Elements("domain").Elements("domain").Attributes("class_ref").FirstValue();

        /// <summary>
        /// Creates a new Property object within the given context, backed by the given XML element.
        /// </summary>
        /// <param name="context">The context for this element</param>
        /// <param name="element">The XML element with the data for this element</param>
        /// <param name="parent">The parent element</param>
        public Property(Context context, XElement element, Model.IElement? parent = null)
            : base(context, element, parent)
        {
        }

        public Property(Property property, Model.IElement? parent)
            : base(property, parent)
        {
            Unit = property.Unit;
        }

        public bool IsClassReferenceType() => Type == "ontoml:CLASS_REFERENCE_TYPE_Type";

        public Model.IElement? ResolveReference()
        {
            if (IsClassReferenceType())
            {
                if (Hierarchy.Contains(ClassReferenceIrdi))
                {
                    Console.WriteLine($"Circle detected!  Id = {Id}, ClassReferenceId = {ClassReferenceIrdi}, " +
                        $"Hierarchy = {string.Join(" --> ", Hierarchy)}");
                    Log.Singleton.Info("Circle detected during import");
                }
                else
                {
                    return Context.GetElement<Class>(ClassReferenceIrdi);
                }
            }
            return this;
        }

        /// <inheritdoc/>
        public override bool ImportSubmodelElementsInto(Aas.IEnvironment env,
            Aas.IReferable parent)
        {
            if (!IsSelected)
                return false;

            var data = GetIec61360Data();
            var property = Iec61360Utils.CreateProperty(env, data, GetValueType(data.DataType));
            parent.Add(property);
            return true;
        }

        protected override Iec61360Data GetIec61360Data()
        {
            var data = base.GetIec61360Data();
            data.DataType = GetDataType(Type);
            data.ShortName = ShortName;
            data.Unit = Unit;
            data.UnitIrdi = UnitIrdi;
            return data;
        }

        public override Dictionary<string, string> GetDetails()
        {
            var details = base.GetDetails();
            details.Add("Short Name", ShortName.GetDefault());
            return details;
        }

        private static string GetDataType(string type)
        {
            // TODO (krahlro-sick, 2021-02-23): This logic is copied from EclassUtils.GenerateConceptDescription -- does
            // it handle all possible values?
            var lowerType = type.ToLower();
            foreach (var aasType in AdminShellUtil.GetEnumValues<Aas.DataTypeIec61360>())
            {
                if (lowerType.Contains(aasType.ToString().ToLower()))
                    return aasType.ToString();
            }
            return string.Empty;
        }

        private static string GetValueType(string dataType)
        {
            switch (dataType)
            {
                case "STRING":
                case "STRING_TRANSLATABLE":
                    return "string";
                case "REAL_MEASURE":
                case "REAL_COUNT":
                case "REAL_CURRENCY":
                    return "double";
                case "INTEGER_MEASURE":
                case "INTEGER_COUNT":
                case "INTEGER_CURRENCY":
                    return "int";
                case "BOOLEAN":
                    return "boolean";
                case "URL":
                case "RATIONAL":
                case "RATIONAL_MEASURE":
                    return "string";
                case "TIME":
                case "TIMESTAMP":
                    return "time";
                case "DATE":
                    return "date";
            }
            return string.Empty;
        }
    }

    internal static class Namespaces
    {
        public static XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

        public static XNamespace OntoML { get; } = "urn:iso:std:iso:is:13584:-32:ed-1:tech:xml-schema:ontoml";

        public static XNamespace UnitsML { get; } = "urn:oasis:names:tc:unitsml:schema:xsd:UnitsMLSchema-1.0";

        public static XNamespace Dic20 = "urn:eclass:xml-schema:dictionary:2.0";

        public static XNamespace Dic30 = "urn:eclass:xml-schema:dictionary:3.0";

        public static XNamespace Hea20 = "urn:eclass:xml-schema:header:2.0";

        public static XNamespace Hea30 = "urn:eclass:xml-schema:header:3.0";
    }

    internal static class Extensions
    {
        public static string FirstValue(this IEnumerable<XElement> elements)
            => elements.Values().DefaultIfEmpty("").First();

        public static string FirstValue(this IEnumerable<XAttribute> attributes)
            => attributes.Values().DefaultIfEmpty("").First();

        public static IEnumerable<string> Values(this IEnumerable<XElement> elements)
            => elements.Select(e => e.Value);

        public static IEnumerable<string> Values(this IEnumerable<XAttribute> attributes)
            => attributes.Select(a => a.Value);
    }
}
