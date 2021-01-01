using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SubtitleEditor
{
    public class Subtitle : BindableBase
    {
        public string FilePath { get; private set; }

        public ObservableCollection<SubtitlePart> FilteredParts { get; private set; } = new ObservableCollection<SubtitlePart>();

        public List<SubtitlePart> AllParts { get; private set; } = new List<SubtitlePart>();

        private SubtitlePart selectedPart;

        public SubtitlePart SelectedPart
        {
            get { return selectedPart; }
            set { 
                if (selectedPart != null)
                    selectedPart.IsSelected = false;
                SetProperty(ref selectedPart, value);
                value.IsSelected = true;
            }
        }

        private bool propogateChanges = true;

        public bool PropogateChanges
        {
            get { return propogateChanges; }
            set { SetProperty(ref propogateChanges, value); }
        }

        public MainWindowViewModel MainWindowVM { get; private set; }

        public Subtitle(MainWindowViewModel mainWindowVM, string filePath)
        {
            MainWindowVM = mainWindowVM;
            FilePath = filePath;
            ProcessFile();

            //SelectedPart = AllParts.FirstOrDefault();

        }

        private void ProcessFile()
        {

            string[] allLines = File.ReadAllLines(FilePath);

            var i = 0;
            Regex indexRegex = new Regex(@"(\d+)");
            Regex timingsRegex = new Regex(@"(\d+):(\d+):(\d+),*(\d*)\s*-->\s*(\d+):(\d+):(\d+),*(\d*)");

            while (i < allLines.Length)
            {

                if (!string.IsNullOrEmpty(allLines[i]))
                {

                    SubtitlePart part = new SubtitlePart(this);

                    //Read Index
                    Match indexMatch = indexRegex.Match(allLines[i]);
                    if (indexMatch != null && indexMatch.Success)
                    {
                        if (indexMatch.Groups.Count > 0)
                        {
                            if (indexMatch.Groups[0].Success)
                            {
                                part.Index = indexMatch.Groups[0].Value;
                            }
                        }
                    }

                    i++;

                    //Read Timings
                    if (!string.IsNullOrEmpty(allLines[i]))
                    {
                        Match timingsMatch = timingsRegex.Match(allLines[i]);

                        if (timingsMatch != null && timingsMatch.Success)
                        {
                            if (timingsMatch.Groups.Count >= 9)
                            {
                                part.PopulateTiming(timingsMatch.Groups);
                            }
                        }
                    }

                    i++;


                    //Read Content (multiple lines)

                    while (i < allLines.Length && !string.IsNullOrEmpty(allLines[i]))
                    {
                        var line = allLines[i].TrimEnd();
                        part.AddContentLine(MainWindowVM.ReplaceProfanePhrases(line));
                        i++;
                    }
                    
                    part.SerialNumber = AllParts.Count + 1;

                    //Add the part
                    AllParts.Add(part);

                }

                i++; //Added for empty lines

            }


        }

        public void Save(string filePath)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var part in AllParts)
            {
                sb.AppendLine(part.Index);
                sb.Append(part.ModifiedStartTiming.Text);
                sb.Append(" --> ");
                sb.AppendLine(part.ModifiedEndTiming.Text);
                part.ContentLines.ForEach(l=>sb.AppendLine(l));
                sb.AppendLine();
            }
            
            File.WriteAllText(filePath, sb.ToString());
        }

    }

    public class SubtitlePart : BindableBase
    {
        public DelegateCommand<object> EditCommand { get; set; }

        private int seq;

        public int SerialNumber
        {
            get { return seq; }
            set { SetProperty(ref seq, value); }
        }

        private string index;

        public string Index
        {
            get { return index; }
            set { SetProperty(ref index, value); }
        }

        public bool IsStartModified
        {
            get { return StartTiming.TimeSpan != ModifiedStartTiming.TimeSpan ; }
        }

        public bool IsEndModified
        {
            get { return EndTiming.TimeSpan != ModifiedEndTiming.TimeSpan; }
        }

        public List<string> ContentLines { get; private set; } = new List<string>();

        public SubtitlePartTiming StartTiming { get; private set; }

        public SubtitlePartTiming EndTiming { get; private set; }

        private SubtitlePartTiming modifiedStartTiming;
        
        public SubtitlePartTiming ModifiedStartTiming
        {
            get { return modifiedStartTiming; }
            private set { SetProperty(ref modifiedStartTiming, value); }
        }

        private SubtitlePartTiming modifiedEndTiming;

        public SubtitlePartTiming ModifiedEndTiming
        {
            get { return modifiedEndTiming; }
            private set { SetProperty(ref modifiedEndTiming, value); }
        }
        
        public TimeSpan Duration { get; set; }

        public Subtitle Subtitle { get; private set; }
        
        public string Lines
        {
            get { return string.Join("\n", ContentLines); }
            set { ContentLines = value.Split( '\n').ToList(); OnPropertyChanged("Lines"); }
        }

        private bool isSelected;

        public bool IsSelected
        {
            get { return isSelected; }
            set { SetProperty(ref isSelected, value); }
        }


        public SubtitlePart(Subtitle subtitle)
        {
            Subtitle = subtitle;
            EditCommand = new DelegateCommand<object>(EditCommandExecute);
        }

        private void EditCommandExecute(object parameter)
        {
            Subtitle.SelectedPart = this;
            Subtitle.MainWindowVM.MainWindow.ShowEditSubtitleFlyout(this);
        }

        private void ModifiedEndTiming_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //Add the difference to the subsequent modified timings
            if (e.PropertyName == "Hours" || e.PropertyName == "Minutes" || e.PropertyName == "Seconds" || e.PropertyName == "Milliseconds")
            {
                ModifiedEndTiming.RaisePropertyChanged("Text");
            }
            else if (e.PropertyName == "Text")
            {
                OnPropertyChanged("IsEndModified");
            }

        }

        private void ModifiedStartTiming_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //Add the difference to the subsequent modified timings
            if (e.PropertyName == "Hours" || e.PropertyName == "Minutes" || e.PropertyName == "Seconds" || e.PropertyName == "Milliseconds")
            {

                ModifiedStartTiming.RaisePropertyChanged("Text");
                var delta = ModifiedStartTiming.Delta;
                
                ModifiedEndTiming.AddDuration(delta);
                ModifiedEndTiming.RaisePropertyChanged("Text", "Hours", "Minutes", "Seconds", "Milliseconds");

                if (Subtitle.PropogateChanges)
                {

                    for (int i = SerialNumber; i < Subtitle.AllParts.Count; i++)
                    {

                        Subtitle.AllParts[i].ModifiedStartTiming.AddDuration(delta);
                        Subtitle.AllParts[i].ModifiedEndTiming.AddDuration(delta);

                    }
                    
                }

            }
            else if (e.PropertyName == "Text")
            {
                OnPropertyChanged("IsStartModified");
            }

            //Set Timing

        }

        public void PopulateTiming(GroupCollection groups)
        {
            StartTiming = SubtitlePartTiming.Create(groups, 0);
            ModifiedStartTiming = SubtitlePartTiming.Create(groups, 0);
            EndTiming = SubtitlePartTiming.Create(groups, 4);
            ModifiedEndTiming = SubtitlePartTiming.Create(groups, 4);

            Duration = EndTiming - StartTiming;

            //Track modified start timing
            ModifiedStartTiming.PropertyChanged += ModifiedStartTiming_PropertyChanged;
            ModifiedEndTiming.PropertyChanged += ModifiedEndTiming_PropertyChanged;

        }

        public void AddContentLine(string contentLine)
        {
            ContentLines.Add(contentLine);
        }

    }

    public class SubtitlePartTiming : BindableBase
    {
        private int hours;

        public int Hours
        {
            get { return hours; }
            set { Delta = new TimeSpan(0, value- hours, 0, 0, 0); SetProperty(ref hours, value); }
        }

        private int minutes;

        public int Minutes
        {
            get { return minutes; }
            set { Delta = new TimeSpan(0, 0, value - minutes, 0, 0); SetProperty(ref minutes, value); }
        }

        private int seconds;

        public int Seconds
        {
            get { return seconds; }
            set { Delta = new TimeSpan(0, 0, 0, value - seconds, 0); SetProperty(ref seconds, value); }
        }

        private int milliseconds;

        public int Milliseconds
        {
            get { return milliseconds; }
            set { Delta = new TimeSpan(0, 0, 0, 0, value-milliseconds); SetProperty(ref milliseconds, value); }
        }

        public TimeSpan TimeSpan
        {
            get
            {
                return new TimeSpan(0, hours, minutes, seconds, milliseconds);
            }
        }

        public TimeSpan Delta
        {
            get;
            private set;
        }
                
        public TimeSpan GetDifference(SubtitlePartTiming timingToSubtract)
        {
            return TimeSpan - timingToSubtract.TimeSpan;
        }

        public static TimeSpan operator -(SubtitlePartTiming t1, SubtitlePartTiming t2)
        {
            return t1.TimeSpan - t2.TimeSpan;
        }

        public void AddDuration(TimeSpan ts)
        {
            TimeSpan t2 = TimeSpan.Add(ts);

            hours = t2.Hours;
            minutes = t2.Minutes;
            seconds = t2.Seconds;
            milliseconds = t2.Milliseconds;

            OnPropertyChanged("Text");
        }

        private SubtitlePartTiming()
        {
        }

        public static SubtitlePartTiming Create(GroupCollection groups, int startIndex)
        {

            SubtitlePartTiming timing = new SubtitlePartTiming();

            if (groups[startIndex + 1].Success)
            {
                timing.Hours = Convert.ToInt32(groups[startIndex + 1].Value);
            }
            if (groups[startIndex + 2].Success)
            {
                timing.Minutes = Convert.ToInt32(groups[startIndex + 2].Value);
            }
            if (groups[startIndex + 3].Success)
            {
                timing.Seconds = Convert.ToInt32(groups[startIndex + 3].Value);
            }
            if (groups[startIndex + 4].Success)
            {
                timing.Milliseconds = Convert.ToInt32(groups[startIndex + 4].Value);
            }

            return timing;

        }

        public string Text
        {
            get { return $"{Hours:00}:{Minutes:00}:{Seconds:00},{Milliseconds:000}"; }
        }

        public void RaisePropertyChanged( params string[] properties )
        {
            foreach (var prop in properties)
            {
                OnPropertyChanged(prop);
            }
            
        }


    }

    public class FilterCriteria : BindableBase
    {

        public DelegateCommand<object> FilterCommand { get; private set; } 

        public Subtitle Subtitle { get; set; }

        public FilterCriteria(Subtitle subtitle)
        {
            Subtitle = subtitle;
            FilterCommand = new DelegateCommand<object>(Filter);
        }

        private string searchCriteria = string.Empty;

        public string SubtitleText
        {
            get { return searchCriteria; }
            set { SetProperty(ref searchCriteria, value); Filter(); }
        }

        private TimeSpan startTime = TimeSpan.Zero;

        public TimeSpan StartTime
        {
            get { return startTime; }
            set { SetProperty(ref startTime, value.Add(new TimeSpan(0, 0, 0, 0, -1 * value.Milliseconds))); Filter(); }
        }

        private TimeSpan endTime = TimeSpan.Zero;

        public TimeSpan EndTime
        {
            get { return endTime; }
            set { SetProperty(ref endTime, value.Add(new TimeSpan(0, 0, 0, 0, -1 * value.Milliseconds))); Filter(); }
        }

        private void Filter(object parameter=null)
        {
            IEnumerable<SubtitlePart> filteredParts = Subtitle.AllParts;

            if(EndTime != TimeSpan.Zero)
                filteredParts = Subtitle.AllParts.Where(p => (string.IsNullOrEmpty(SubtitleText) || p.Lines.ToLower().Contains(SubtitleText.ToLower())) && p.ModifiedStartTiming.TimeSpan >= StartTime && p.ModifiedStartTiming.TimeSpan <= EndTime);

            Subtitle.FilteredParts.Clear();

            foreach (var filteredPart in filteredParts)
            {
                Subtitle.FilteredParts.Add(filteredPart);
            }

            //Subtitle.SelectedPart = Subtitle.FilteredParts.FirstOrDefault();

        }

    }

}
