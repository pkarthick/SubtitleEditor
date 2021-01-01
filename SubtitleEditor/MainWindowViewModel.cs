using Microsoft.Practices.Prism.Mvvm;
using SubtitleEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SubtitleEditor
{
    public class MainWindowViewModel : BindableBase
    {
        public MainWindow MainWindow { get; private set; }

        private Subtitle subtitle;

        public Subtitle Subtitle
        {
            get { return subtitle; }
            set { SetProperty(ref subtitle, value); FilterCriteria = new FilterCriteria(value); OnPropertyChanged("HasSubtitle"); }
        }

        public bool HasSubtitle
        {
            get { return subtitle != null; }
        }

        private FilterCriteria filterCriteria;

        public FilterCriteria FilterCriteria
        {
            get { return filterCriteria; }
            set { SetProperty(ref filterCriteria, value); }
        }

        private Dictionary<string, Regex> profaneRegexDictionary = new Dictionary<string, Regex>();
        private Dictionary<string, string> profaneReplacementDictionary = new Dictionary<string, string>();

        private List<string> ProfanePhrases { get; set; } = new List<string>();

        public MainWindowViewModel(MainWindow mainWindow)
        {
            MainWindow = mainWindow;
            StreamReader streamReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("SubtitleEditor.ProfanePhrases.txt"));
            string fileContent = streamReader.ReadToEnd();

            ProfanePhrases.AddRange(fileContent.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));

            foreach (var phrase in ProfanePhrases)
            {
                profaneRegexDictionary[phrase] = new Regex($"\\b{phrase}s*\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
                profaneReplacementDictionary[phrase] = phrase.Substring(0, 1).PadRight(phrase.Length - 1, '*') + phrase.Substring(phrase.Length - 1);
            }


        }

        public string ReplaceProfanePhrases(string input)
        {
            foreach (var phrase in ProfanePhrases)
            {
                input = profaneRegexDictionary[phrase].Replace(input, profaneReplacementDictionary[phrase]);
            }

            return input;
        }

    }
}
