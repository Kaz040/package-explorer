/*
Copyright (c) 2018-2023 Festo SE & Co. KG <https://www.festo.com/net/de_de/Forms/web/contact_international>
Author: Michael Hoffmeister

This source code is licensed under the Apache License 2.0 (see LICENSE.txt).

This source code may use other Open Source software components (see LICENSE.txt).
*/

using AdminShellNS;
using AnyUi;
using Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Web;
using Aas = AasCore.Aas3_0;

// ReSharper disable UnassignedField.Global

namespace AasxPackageLogic
{
    /// <summary>
    /// This attribute indicates a valid option for JSON or command line
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class OptionDescription : System.Attribute
    {
        public string Description = null;
        public string Cmd = null;
        public string Arg = null;
    }

    /// <summary>
    /// This attribute indicates, that it should e.g. serialized in JSON.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = true)]
    public class SettableOption : System.Attribute
    {
        public string JsonName = null;
    }

    /// <summary>
    /// The Singleton for providing options.
    /// </summary>
    public static class Options
    {
        private static OptionsInformation instance = null;
        private static readonly object padlock = new object();

        /// <summary>
        /// The Singleton for Options
        /// </summary>
        public static OptionsInformation Curr
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = new OptionsInformation();
                    }
                    return instance;
                }
            }
        }

        /// <summary>
        /// Instantaneously replaces the Options singleton instance with the data provided.
        /// </summary>
        /// <param name="io"></param>
        public static void ReplaceCurr(OptionsInformation io)
        {
            lock (padlock)
            {
                instance = io;
            }
        }

        public enum ReportOptionsFormat { Markdown, Html }

        private static string MdEsc(string st)
        {
            st = "" + st;
            st = st.Replace("<", @"\<");
            st = st.Replace("<", @"\<");
            return st;
        }

        private static string HtmlEsc(string st)
        {
            return HttpUtility.HtmlEncode(st);
        }

        public static string ReportOptions(ReportOptionsFormat fmt,
            OptionsInformation options = null)
        {
            var sb = new StringBuilder();

            //
            // small lambdas
            //

            Action appendTableHeaderMd = () =>
            {
                sb.AppendLine($"| {"JSON option",-35} | {"Command line",-20} " +
                                    $"| {"Argument",-20} | Description |");
                sb.AppendLine($"|-{new String('-', 35)}-|-{new String('-', 20)}-" +
                    $"|-{new String('-', 20)}-|-------------|");
            };

            Action<string, string, string, string> appendTableRowMd = (json, cmd, arg, description) =>
            {
                sb.AppendLine($"| {MdEsc(json),-35} | {MdEsc(cmd),-20} " +
                    $"| {MdEsc(arg),-20} | {MdEsc(description)} |");
            };

            Action appendTableHeaderHtml = () =>
            {
                sb.AppendLine($"<tr><th>JSON option</th><th>Command line</th>" +
                    $"<th>Argument</th><th>Description</th></tr>");
            };

            Action<string, string, string, string> appendTableRowHtml = (json, cmd, arg, description) =>
            {
                sb.AppendLine($"<tr><td>{HtmlEsc(json)}</td><td>{HtmlEsc(cmd)}</td>" +
                    $"<td>{HtmlEsc(arg)}</td><td>{HtmlEsc(description)}</td></tr>");
            };

            //
            // Regular options
            //

            if (fmt == ReportOptionsFormat.Markdown)
            {
                sb.AppendLine("# Regular options for JSON and command line");
                sb.AppendLine();
                sb.AppendLine(AdminShellUtil.CleanHereStringWithNewlines(
                    @"The following options can be used either directly in the command line of the 
                    exectable or in a JSON-file for configuration (via the ""-read-json"" option)."));
                sb.AppendLine();
            }

            if (fmt == ReportOptionsFormat.Html)
            {
                var htmlHeader = AdminShellUtil.CleanHereStringWithNewlines(
                    @"<!doctype html>
                    <html lang=en>
                    <head>
                    <style>
                    body {
                      background-color: #FFFFE0;
                      font-size: small;
                      font-family: Arial, Helvetica, sans-serif;
                    }
                    table {
                      font-family: arial, sans-serif;
                      border-collapse: collapse;
                      width: 100%;
                    }
                    tr:nth-child(odd) {
                      background-color: #FFFFF8;
                    }
                    td, th {
                      border: 1px solid #dddddd;
                      text-align: left;
                      padding: 8px;
                    }
                    </style>
                    <meta charset=utf-8>
                    <title>Options information</title>
                    </head>
                    <body>
                    <h1>Regular options for JSON and command line</h1>
                    <h4>The following options can be used either directly in the command line of the 
                    exectable or in a JSON-file for configuration (via the ""-read-json"" option).</h4>
                    <table>");

                sb.AppendLine(htmlHeader);
            }

            var fields = typeof(OptionsInformation).GetFields(BindingFlags.Public | BindingFlags.Instance);
            var first = true;
            foreach (var fi in fields)
            {
                foreach (var fia in fi.GetCustomAttributes(typeof(OptionDescription), true))
                    if (fia is OptionDescription fiaod)
                    {
                        if (fmt == ReportOptionsFormat.Markdown)
                        {
                            if (first)
                            {
                                first = false;
                                appendTableHeaderMd();
                            }
                            appendTableRowMd(fi.Name, fiaod.Cmd, fiaod.Arg, fiaod.Description);
                        }

                        if (fmt == ReportOptionsFormat.Html)
                        {
                            if (first)
                            {
                                first = false;
                                appendTableHeaderHtml();
                            }
                            appendTableRowHtml(fi.Name, fiaod.Cmd, fiaod.Arg, fiaod.Description);
                        }
                    }
            }

            sb.AppendLine("</table>");

            //
            // Special options
            //

            if (fmt == ReportOptionsFormat.Markdown)
            {
                sb.AppendLine("# Special options for JSON and command line");
                sb.AppendLine();
                sb.AppendLine(AdminShellUtil.CleanHereStringWithNewlines(
                    @"The following options are also be provided."));
                sb.AppendLine();

                appendTableHeaderMd();
                appendTableRowMd("", "-read-json", "<path>", "Reads a JSON formatted options file.");
                appendTableRowMd("", "-write-json", "<path>", "Writes the currently loaded options " +
                    "into a JSON formatted file.");
            }

            if (fmt == ReportOptionsFormat.Html)
            {
                sb.AppendLine("<h1>Special options for JSON and command line</h1>");
                sb.AppendLine("<h4>The following options are also be provided.</h4>");
                sb.AppendLine("<table>");
                appendTableHeaderHtml();
                appendTableRowHtml("", "-read-json", "<path>", "Reads a JSON formatted options file.");
                appendTableRowHtml("", "-write-json", "<path>", "Writes the currently loaded options " +
                    "into a JSON formatted file.");
                sb.AppendLine("</table>");
            }

            sb.AppendLine();

            //
            // Report current options?
            //

            if (options != null)
            {
                var jsonStr = JsonConvert.SerializeObject(options, Formatting.Indented);

                if (fmt == ReportOptionsFormat.Markdown)
                {
                    sb.AppendLine("# Current options");
                    sb.AppendLine();
                    sb.AppendLine(AdminShellUtil.CleanHereStringWithNewlines(
                        @"The following options are currently loaded or set by default."));
                    sb.AppendLine();

                    sb.AppendLine("```");
                    sb.AppendLine(jsonStr);
                    sb.AppendLine("```");

                    sb.AppendLine();
                }

                sb.AppendLine("<h1>Current options</h1>");
                sb.AppendLine("<h4>The following options are currently loaded or set by default.</h4>");

                sb.AppendLine("<pre>");
                sb.AppendLine(HtmlEsc(jsonStr));
                sb.AppendLine("</pre>");
            }

            //
            // Footer
            //

            if (fmt == ReportOptionsFormat.Markdown)
            {
                sb.AppendLine("</body>");
                sb.AppendLine("</html>");
            }

            //
            // End of report
            //

            return sb.ToString();
        }
    }


    /// <summary>
    /// This class holds the command line options. An "Options" Singleton will provide an instance of it.
    /// </summary>
    public class OptionsInformation
    {

        [OptionDescription(Description = "This file shall be loaded as main package at start of application",
            Cmd = "-aasx-to-load", Arg = "<path>")]
        [SettableOption]
        public string AasxToLoad = null;

        [OptionDescription(Description = "This file shall be loaded as aux package at start of application",
            Cmd = "-aux-to-load", Arg = "<path>")]
        public string AuxToLoad = null;

        [OptionDescription(Description = "if not -1, the left of window",
            Cmd = "-left", Arg = "<pixel>")]
        public int WindowLeft = -1;

        [OptionDescription(Description = "if not -1, the top of window",
            Cmd = "-top", Arg = "<pixel>")]
        public int WindowTop = -1;

        [OptionDescription(Description = "if not -1, the width of window",
            Cmd = "-width", Arg = "<pixel>")]
        public int WindowWidth = -1;

        [OptionDescription(Description = "if not -1, the height of window",
            Cmd = "-height", Arg = "<pixel>")]
        public int WindowHeight = -1;

        [OptionDescription(Description = "if set, then maximize window on application startup",
            Cmd = "-maximized")]
        public bool WindowMaximized = false;

        [OptionDescription(Description = "Template string for the id of an AAS. " +
            "Could contain up to 16 placeholders of: " +
            "D = decimal digit, X = hex digit, A = alphanumerical digit",
            Cmd = "-id-aas", Arg = "<string>")]
        public string TemplateIdAas = "https://example.com/ids/aas/DDDD_DDDD_DDDD_DDDD";

        [OptionDescription(Description = "Template string for the id of an aaset. " +
            "Could contain up to 16 placeholders of: " +
            "D = decimal digit, X = hex digit, A = alphanumerical digit",
            Cmd = "-id-asset", Arg = "<string>")]
        public string TemplateIdAsset = "https://example.com/ids/asset/DDDD_DDDD_DDDD_DDDD";

        [OptionDescription(Description = "Template string for the id of an submodel of kind instance. " +
            "Could contain up to " +
            "16 placeholders of: D = decimal digit, X = hex digit, A = alphanumerical digit",
            Cmd = "-id-sm-instance", Arg = "<string>")]
        public string TemplateIdSubmodelInstance = "https://example.com/ids/sm/DDDD_DDDD_DDDD_DDDD";

        [OptionDescription(Description = "Template string for the id of an submodel of kind type. " +
            "Could contain up to " +
            "16 placeholders of: D = decimal digit, X = hex digit, A = alphanumerical digit",
            Cmd = "-id-sm-template", Arg = "<string>")]
        public string TemplateIdSubmodelTemplate = "https://example.com/ids/sm/DDDD_DDDD_DDDD_DDDD";

        [OptionDescription(Description = "Template string for the id of a concept description. " +
            "Could contain up to 16 " +
            "placeholders of: D = decimal digit, X = hex digit, A = alphanumerical digit",
            Cmd = "-id-cd", Arg = "<string>")]
        public string TemplateIdConceptDescription = "https://example.com/ids/cd/DDDD_DDDD_DDDD_DDDD";

        [OptionDescription(Description = "Link ConceptDescriptions by ModelReferences",
            Cmd = "-model-ref-cd")]
        public bool ModelRefCd = false;

        [OptionDescription(Description = "Path to ECLASS files",
            Cmd = "-eclass", Arg = "<path>")]
        public string EclassDir = null;

        [OptionDescription(Description =
            "Path to the directory with the default sources for the Dictionary Import " +
            "feature (see AasxDictionaryImport).  If this option is not set, the current working directory " +
            "is used.",
            Cmd = "-dict-import-dir", Arg = "<path>")]
        public string DictImportDir = System.IO.Directory.GetCurrentDirectory();

        [OptionDescription(Description = "Path to an image to be displayed as logo",
            Cmd = "-logo", Arg = "<path>")]
        public string LogoFile = null;

        [OptionDescription(Description = "Path to JSON file defining qualifier presets.",
            Cmd = "-qualifiers", Arg = "<path>")]
        public string QualifiersFile = null;

        [OptionDescription(Description = "Path to JSON file defining IdentifierKeyValuePair presets.",
            Cmd = "-idpairs", Arg = "<path>")]
        public string IdentifierKeyValuePairsFile = null;

        [OptionDescription(Description = "Path to JSON file defining Referable.extension presets.",
            Cmd = "-extpreset", Arg = "<path>")]
        public string ExtensionsPresetFile = null;

        [OptionDescription(Description = "Path to JSON file defining data specification presets.",
            Cmd = "-dataspecpreset", Arg = "<path>")]
        public string DataSpecPresetFile = null;

        [OptionDescription(Description = "Path to a JSON, defining a set of AasxPackage-Files, which serve as " +
            "repository",
            Cmd = "-aasxrepo", Arg = "<path>")]
        public string AasxRepositoryFn = null;

        [OptionDescription(Description = "Home address of the content browser on startup, on change of AASX",
            Cmd = "-contenthome", Arg = "<URL>")]
        public string ContentHome = @"https://github.com/admin-shell/io/blob/master/README.md";

        [OptionDescription(Description = "If unset, use transparent flyover dialogs, where possible",
            Cmd = "-noflyouts")]
        public bool UseFlyovers = true;

        [OptionDescription(Description = "If other then -1, then time in ms for the splash window to " +
            "stay on the screen.",
            Cmd = "-splash", Arg = "<milli-secs>")]
        public int SplashTime = -1;

        [OptionDescription(Description = "Fraction of main window with dedicated to left column of screen " +
            "when resizing window.",
            Cmd = "-percentage-left-col", Arg = "Percent 0-100.0")]
        public double PercentageLeftColumn = 20.0;

        [OptionDescription(Description = "Fraction of main window with dedicated to right column of screen " +
            "(content section) when resizing window.",
            Cmd = "-percentage-right-col", Arg = "Percent 0-100.0")]
        public double PercentageRightColumn = 40.0;

        [OptionDescription(Description = "If true, use always internal browser",
            Cmd = "-intbrowse")]
        public bool InternalBrowser = false;

        [OptionDescription(Description = "If true, apply second search operation in ECLASS " +
            "to join multi-language information.",
            Cmd = "-twopass")]
        public bool EclassTwoPass = false;

        [OptionDescription(Description = "If not null, enables backing up XML files of the AAS-ENV in " +
            "some files under BackupDir, which could be relative",
            Cmd = "-backupdir", Arg = "<path>")]
        public string BackupDir = null;

        [OptionDescription(Description = "At max such much different files are used for backing up")]
        public int BackupFiles = 10;

        [OptionDescription(Description = "If set, load and store AASX files via temporary package to " +
            "avoid corruptions. RECOMMENDED!",
            Cmd = "-indirect-load-save")]
        public bool IndirectLoadSave = false;

        [OptionDescription(Description = "Hostname for the REST server. If other than \"localhost\", " +
            "use of admin rights might be required.",
            Cmd = "-resthost", Arg = "<host>")]
        public string RestServerHost = "localhost";

        [OptionDescription(Description = "Port for the REST server. Port numbers below 1023 may not work.",
            Cmd = "-restport", Arg = "<port>")]
        public string RestServerPort = "1111";

        [OptionDescription(Description = "If not null, will retrieve the (default?) options of all instantiated " +
            "plugins and will write this large data set into JSON option file.",
            Cmd = "-write-all-json", Arg = "<path>")]
        public string WriteDefaultOptionsFN = null;

        [OptionDescription(Description =
            "If not null points to the dir, where per user a directory is provided to load/ save files.",
            Cmd = "-user-dir")]
        public string UserDir = null;

        [OptionDescription(Description =
            "Designates the user name. Only [A-Za-z0-9_] allowed. Must be not null in order to access user files.",
            Cmd = "-user-name")]
        public string UserName = null;

        [OptionDescription(Description =
            "If true, will allow the storage of local files. The server functions might restrict this.",
            Cmd = "-allow-local-files")]
        public bool AllowLocalFiles = true;

        [OptionDescription(Description =
            "Designates the default language codes in ISO639-1. This will be used for new language strings " +
            "of uncertain information. Each language separated by comma. First language code is the default one.",
            Cmd = "-default-langs")]
        public string DefaultLangs = "en";

        [OptionDescription(Description =
            "List of language/ country codes offered by the UI. Comma-separated tags of two-digit ISO 639-2 and ISO 3166-1 codes, " +
            "also known (but not equal) to RFC 5464 locales. ",
            Cmd = "-offered-langs")]
        public string OfferedLangs = "en";

        [OptionDescription(Description =
            "Designates the default value of the first text value of a newly created multi language list. " +
            "If left blank, the application will create warnings because violating the AAS specification and " +
            "some AASX export might not work properly.",
            Cmd = "-default-empty-lang-text")]
        public string DefaultEmptyLangText = "";

        [OptionDescription(Description =
            "Designates the default value of the first key of a newly created AAS Reference. If left blank, " +
            "the application will create warnings because violating the AAS specification and some AASX export " +
            "might not work properly.",
            Cmd = "-default-empty-ref-key")]
        public string DefaultEmptyReferenceKey = "";

        [OptionDescription(Description =
            "Path to file to capture all log messages. Will be (re-) created at application startup.",
            Cmd = "-log-file")]
        public string LogFile = null;

        /// <summary>
        /// Enumeration of generic accent color names
        /// </summary>
        public enum ColorNames
        {
            LightAccentColor = 0, DarkAccentColor, DarkestAccentColor, FocusErrorBrush, FocusErrorColor
        };

        [OptionDescription(Description = "Dictionary of override colors")]
        [SettableOption]
        public Dictionary<ColorNames, AnyUiColor> AccentColors =
            new Dictionary<ColorNames, AnyUiColor>();

        public AnyUiColor GetColor(ColorNames c)
        {
            if (AccentColors != null && AccentColors.ContainsKey(c))
                return AccentColors[c];
            return AnyUiColors.Black;
        }

        [OptionDescription(Description =
            "Contains a list of remarks. Intended use: disabling lines of preferences.",
            Cmd = "-rem")]
        public List<string> Remarks = new List<string>();

        [OptionDescription(Description =
            "If not null points to the dir, where plugins are (recursively) searched",
            Cmd = "-plugin-dir")]
        public string PluginDir = null;

        [OptionDescription(Description =
            "Plugin usage prefernces, e.g. WPF or ANYUI",
            Cmd = "-plugin-prefer")]
        public string PluginPrefer = null;

		[OptionDescription(Description =
			"Sorting order of ConceptDescriptions (ListIndex, IdShort, Id, Submodel, SME, Structured)",
			Cmd = "-cd-sort-order")]
		public string CdSortOrder = null;

		[OptionDescription(Description =
            "For such operations as query repository, do load a new AASX file without " +
            "prompting the user.",
            Cmd = "-load-without-prompt")]
        public bool LoadWithoutPrompt = false;

        [OptionDescription(Description =
            "When activated, the UI will check if identifications and other texts are " +
            "starting with schemes like http:// and will render IRIs for them",
            Cmd = "-show-id-as-iri")]
        public bool ShowIdAsIri = false;

        [OptionDescription(Description =
            "When activated, the UI will show verbose information on (secure) connect procedures. " +
            "When de-activated, default answers to questions within these procedures will be given",
            Cmd = "-verbose-connect")]
        public bool VerboseConnect = false;

        [OptionDescription(Description = "When activated, the UI will initially show the event viewer. The use can " +
            "hide the  panel, if required.")]
        public bool ShowEvents = false;

        [OptionDescription(Description = "When activated, the UI will detect SubmodelElements which feature " +
            "Extension of type \"Animate.Args\" and will cyclically animate its values.")]
        public bool AnimateElements = false;

        [OptionDescription(Description = "When activated, the UI will observe AAS events, which are subsequently " +
            "emited by the editor. AAS events are emited, when \"take over\" or load/ save is activiated.")]
        public bool ObserveEvents = false;

        [OptionDescription(Description = "When activated, the UI will compress events, which are emited by " +
            "the editor. ")]
        public bool CompressEvents = false;

		[OptionDescription(Description = "When activated, the UI will detect presence of SMT attributes and " +
            "will perform value checks on AAS elements. This might be slow!")]
		public bool CheckSmtElements = false;

		[OptionDescription(Description = "Default value for the StayConnected options of PackageContainer. " +
            "That is, a loaded container will automatically try receive events, e.g. for value update.",
            Cmd = "-stay-connected")]
        public bool DefaultStayConnected = false;

        [OptionDescription(Description = "CONSTANT for the DefaultUpdatePeriod option.")]
        public const int MinimumUpdatePeriod = 200;

        [OptionDescription(Description = "Default value for the update period in [ms] for StayConnect containers.")]
        public int DefaultUpdatePeriod = 0;

        [OptionDescription(Description = "Preset shown in the file repo connect to AAS repository dialogue")]
        public string DefaultConnectRepositoryLocation = "";

        [OptionDescription(Description = "List of tuples (Name, Text) with presets for available scripts.")]
        public List<AnyUiDialogueDataTextEditor.Preset> ScriptPresets;

        [OptionDescription(Description = "Verbosity level for script execution (0=silent, 2=very verbose).")]
        public int ScriptLoglevel = 2;

        [OptionDescription(Description = "Determins if user is prompted before starting a script..",
            Cmd = "-script-launch-without-prompt")]
        public bool ScriptLaunchWithoutPrompt = false;

        [OptionDescription(Description = "Determins if the script is allowed to execute system commands " +
            "on the shell (CMD.EXE).",
            Cmd = "-script-execute-system")]
        public bool ScriptExecuteSystem = false;

        [OptionDescription(Description = "Filename for scipt to read and execute.",
            Cmd = "-script")]
        public string ScriptFn = "";

        [OptionDescription(Description = "Rest of command line will by treated as script. " +
            "Double quotes to be escaped by backslash. In PowerShell, overall command itself to be " +
            "escaped by single ticks.",
            Cmd = "-cmd")]
        public string ScriptCmd = "";

        [OptionDescription(Description = "May contain different string-based options for stay connect, " +
            "event update mechanisms")]
        public string StayConnectOptions = "";

        [OptionDescription(Description = "Point to a list of SecureConnectPresets for the respective dialogue")]
        [JetBrains.Annotations.UsedImplicitly]
        public Newtonsoft.Json.Linq.JToken SecureConnectPresets;

        [OptionDescription(Description = "Point to a set of options for MQTT publish")]
        [JetBrains.Annotations.UsedImplicitly]
        public Newtonsoft.Json.Linq.JToken MqttPublisherOptions;

        [OptionDescription(Description = "Point to a list of SecureConnectPresets for the respective dialogue")]
        [JetBrains.Annotations.UsedImplicitly]
        public Newtonsoft.Json.Linq.JToken IntegratedConnectPresets;

        public OptionsInformation()
        {
            // lightes blue to light: 0xFFf2f5ffu
            // IDTA red to light: 0xFFFE4F10u
            AccentColors[ColorNames.LightAccentColor] = new AnyUiColor(0xFFDBE2FFu);
            AccentColors[ColorNames.DarkAccentColor] = new AnyUiColor(0xFFc0ccffu);
            AccentColors[ColorNames.DarkestAccentColor] = new AnyUiColor(0xFF0128CBu);
            AccentColors[ColorNames.FocusErrorBrush] = new AnyUiColor(0xFFe53d01u);
            AccentColors[ColorNames.FocusErrorColor] = new AnyUiColor(0xFFe53d01u);
        }

        public class PluginDllInfo
        {
            public string Path;
            public string[] Args;

            [JetBrains.Annotations.UsedImplicitly]
            public Newtonsoft.Json.Linq.JToken Options;

            [JetBrains.Annotations.UsedImplicitly]
            public Newtonsoft.Json.Linq.JToken DefaultOptions;

            public PluginDllInfo() { }

            public PluginDllInfo(string path, string[] args = null)
            {
                this.Path = path;
                if (args != null)
                    this.Args = args;
            }
        }

        [OptionDescription(Description = "Contains a list of tuples (filenames, args) of plugins to be loaded.")]
        [SettableOption]
        public List<PluginDllInfo> PluginDll = new List<PluginDllInfo>();

        /// <summary>
        /// Will save options to a file. Catches exceptions.
        /// </summary>
        public static void WriteJson(OptionsInformation optionsInformation, string filename)
        {
            // execute in-line, in order to represent to correct order to the human operator
            try
            {
                var jsonStr = JsonConvert.SerializeObject(optionsInformation, Formatting.Indented);
                System.IO.File.WriteAllText(filename, jsonStr);
            }
            catch (Exception ex)
            {
                Log.Singleton.Error(ex, $"When writing options to a JSON file: {filename}");
            }
        }

        /// <summary>
        /// Will read options from a file into the given instance.
        /// </summary>
        public static void ReadJson(string fn, OptionsInformation optionsInformation)
        {
            try
            {
                var jsonStr = System.IO.File.ReadAllText(fn);
                JsonConvert.PopulateObject(jsonStr, optionsInformation);
            }
            catch (Exception ex)
            {
                Log.Singleton.Error(ex, "When reading options JSON file");
            }
        }

        public static void ParseArgs(string[] args, OptionsInformation optionsInformation)
        {
            // This is a sweep line for plugin arguments.
            var pluginArgs = new List<string>();

            for (int index = 0; index < args.Length; index++)
            {
                var arg = args[index].Trim().ToLower();
                var morearg = (args.Length - 1) - index;

                // flags
                if (arg == "-maximized")
                {
                    optionsInformation.WindowMaximized = true;
                    continue;
                }
                if (arg == "-noflyouts")
                {
                    optionsInformation.UseFlyovers = false;
                    continue;
                }
                if (arg == "-intbrowse")
                {
                    optionsInformation.InternalBrowser = true;
                    continue;
                }
                if (arg == "-twopass")
                {
                    optionsInformation.EclassTwoPass = true;
                    continue;
                }
                if (arg == "-indirect-load-save")
                {
                    optionsInformation.IndirectLoadSave = true;
                    continue;
                }
                if (arg == "-load-without-prompt")
                {
                    optionsInformation.LoadWithoutPrompt = true;
                    continue;
                }
                if (arg == "-show-id-as-iri")
                {
                    optionsInformation.ShowIdAsIri = true;
                    continue;
                }
                if (arg == "-verbose-connect")
                {
                    optionsInformation.VerboseConnect = true;
                    continue;
                }
                if (arg == "-stay-connected")
                {
                    optionsInformation.DefaultStayConnected = true;
                    continue;
                }

                // commands, which are executed on the fly ..
                if (arg == "-read-json" && morearg > 0)
                {
                    // parse
                    var fn = System.IO.Path.GetFullPath(args[index + 1]);
                    index++;

                    // execute in-line, in order to represent to correct order to the human operator
                    OptionsInformation.ReadJson(fn, optionsInformation);

                    // next arg
                    continue;
                }
                if (arg == "-write-json" && morearg > 0)
                {
                    // parse
                    var filename = System.IO.Path.GetFullPath(args[index + 1]);
                    index++;

                    // do
                    OptionsInformation.WriteJson(optionsInformation, filename);

                    // next arg
                    continue;
                }

                // options
                if (arg == "-left" && morearg > 0)
                {
                    if (Int32.TryParse(args[index + 1], out int i))
                        optionsInformation.WindowLeft = i;
                    index++;
                    continue;
                }
                if (arg == "-top" && morearg > 0)
                {
                    if (Int32.TryParse(args[index + 1], out int i))
                        optionsInformation.WindowTop = i;
                    index++;
                    continue;
                }
                if (arg == "-width" && morearg > 0)
                {
                    if (Int32.TryParse(args[index + 1], out int i))
                        optionsInformation.WindowWidth = i;
                    index++;
                    continue;
                }
                if (arg == "-height" && morearg > 0)
                {
                    if (Int32.TryParse(args[index + 1], out int i))
                        optionsInformation.WindowHeight = i;
                    index++;
                    continue;
                }

                if (arg == "-id-aas" && morearg > 0)
                {
                    optionsInformation.TemplateIdAas = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-id-asset" && morearg > 0)
                {
                    optionsInformation.TemplateIdAsset = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-id-sm-template" && morearg > 0)
                {
                    optionsInformation.TemplateIdSubmodelTemplate = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-id-sm-instance" && morearg > 0)
                {
                    optionsInformation.TemplateIdSubmodelInstance = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-id-cd" && morearg > 0)
                {
                    optionsInformation.TemplateIdConceptDescription = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-eclass" && morearg > 0)
                {
                    optionsInformation.EclassDir = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-dict-import-dir" && morearg > 0)
                {
                    optionsInformation.DictImportDir = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-qualifiers" && morearg > 0)
                {
                    optionsInformation.QualifiersFile = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-logo" && morearg > 0)
                {
                    optionsInformation.LogoFile = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-aasxrepo" && morearg > 0)
                {
                    optionsInformation.AasxRepositoryFn = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-contenthome" && morearg > 0)
                {
                    optionsInformation.ContentHome = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-splash" && morearg > 0)
                {
                    if (Int32.TryParse(args[index + 1], out int i))
                        optionsInformation.SplashTime = i;
                    index++;
                    continue;
                }
                if (arg == "-options" && morearg > 0)
                {
                    string pathToOptions = args[index + 1];
                    Log.Singleton.Info(
                        $"Parsing options from a non-default options file: {pathToOptions}");
                    var fullFilename = System.IO.Path.GetFullPath(pathToOptions);
                    OptionsInformation.TryReadOptionsFile(fullFilename, optionsInformation);

                    index++;
                    continue;
                }
                if (arg == "-backupdir" && morearg > 0)
                {
                    optionsInformation.BackupDir = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-resthost" && morearg > 0)
                {
                    optionsInformation.RestServerHost = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-restport" && morearg > 0)
                {
                    optionsInformation.RestServerPort = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-rem" && morearg > 0)
                {
                    // Add one argument to the plugin list
                    optionsInformation.Remarks.Add(args[index + 1]);
                    index++;
                    continue;
                }
                if (arg == "-write-all-json" && morearg > 0)
                {
                    // will be executed very late!
                    optionsInformation.WriteDefaultOptionsFN = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-plugin-dir" && morearg > 0)
                {
                    optionsInformation.PluginDir = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-script" && morearg > 0)
                {
                    optionsInformation.ScriptFn = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-aasx-to-load" && morearg > 0)
                {
                    optionsInformation.AasxToLoad = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-aux-to-load" && morearg > 0)
                {
                    optionsInformation.AuxToLoad = args[index + 1];
                    index++;
                    continue;
                }
                if (arg == "-cmd" && morearg > 0)
                {
                    // consume all args starting with "-cmd" into one command line
                    // Note: the reason for this implementation is, that it seems unreasonably 
                    // difficult to pass a C# like script with double quoted inner strings via
                    // the command line.
                    /// An example would be: 
                    for (int i = index + 1; i < args.Length; i++)
                        optionsInformation.ScriptCmd += args[i] + " ";

                    // virtuall stop the parsing
                    index = args.Length;
                    break; ;
                }
                if (arg == "-log-file" && morearg > 0)
                {
                    optionsInformation.LogFile = args[index + 1];
                    index++;
                    continue;
                }

                // Sweep-line options for plugins and DLL path
                if (arg == "-p" && morearg > 0)
                {
                    // Add exactly one following argument to the sweep line of plugin arguments
                    pluginArgs.Add(args[index + 1]);
                    index += 1;
                    continue;
                }
                if (arg == "-dll" && morearg > 0)
                {
                    // Process and reset the sweep line
                    optionsInformation.PluginDll.Add(
                        new PluginDllInfo(args[index + 1], pluginArgs.ToArray()));
                    pluginArgs.Clear();
                    index++;
                    continue;
                }
                if (arg == "-update-period" && morearg > 0)
                {
                    if (Int32.TryParse(args[index + 1], out int i))
                        optionsInformation.DefaultUpdatePeriod = i;
                    index++;
                    continue;
                }

                // Colors
                {
                    var found = false;
                    for (int i = 0; i < 10; i++)
                        if (arg == $"-c{i:0}" && morearg > 0)
                        {
                            // ReSharper disable PossibleNullReferenceException
                            try
                            {
                                var c = AnyUiColor.FromString(args[index + 1].Trim());
                                optionsInformation.AccentColors.Add((ColorNames)i, c);
                            }
                            catch (Exception ex)
                            {
                                AdminShellNS.LogInternally.That.SilentlyIgnoredError(ex);
                            }
                            // ReSharper enable PossibleNullReferenceException

                            index++;
                            found = true;
                        }
                    if (found)
                        continue;
                }

                // if come to this point and obviously not an option, take this as load argument
                // allow for more options to come (motivation: allow "-write-json options.json" to be the last argument)
                if (!arg.StartsWith("-"))
                {
                    if (System.IO.File.Exists(args[index]))
                        optionsInformation.AasxToLoad = args[index];
                }
            }
        }

        public static void TryReadOptionsFile(string filename, OptionsInformation optionsInformation)
        {
            try
            {
                var optionsTxt = System.IO.File.ReadAllText(filename);
                var argsFromFile = optionsTxt.Split(
                    new[] { '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                OptionsInformation.ParseArgs(argsFromFile, optionsInformation);
            }
            catch (Exception ex)
            {
                Log.Singleton.Error(ex, "Reading options file: " + filename);
            }
        }

        //
        // some more helpers accessing the options attributes
        //

        /// <summary>
        /// Interprets the options and returns proper value.
        /// In the future: allow different key types?
        /// </summary>
        public Aas.IReference GetDefaultEmptyReference()
        {
            return ExtendReference.CreateFromKey(
                KeyTypes.GlobalReference,
                Options.Curr.DefaultEmptyReferenceKey);
        }

        //This method has ben added to make Event.Observed as model reference
        public Aas.IReference GetDefaultEmptyModelReference()
        {
            return ExtendReference.CreateFromKey(
                ReferenceTypes.ModelReference,
                KeyTypes.Referable,
                Options.Curr.DefaultEmptyReferenceKey);
        }

        public AdminShellUtil.CreateSubmodelElementDefaultHelper GetCreateDefaultHelper()
        {
            return new AdminShellUtil.CreateSubmodelElementDefaultHelper()
            {
                CreateDefaultReference = GetDefaultEmptyReference,
            };
        }
    }
}
