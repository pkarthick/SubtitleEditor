using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SubtitleEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindowViewModel MainWindowVM { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            MainWindowVM = new MainWindowViewModel(this);
            DataContext = MainWindowVM;
        }

        private void OpenSubtitle_Click(object sender, RoutedEventArgs e)
        {
            SubtitleEditorFlyout.IsOpen = false;

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Subtitle (*.srt)|*.srt";

            bool? result = dialog.ShowDialog();

            if (result.HasValue && result.Value)
            {
                MainWindowVM.Subtitle = new Subtitle(MainWindowVM, dialog.FileName);
                

                var start = TimeSpan.Zero;
                var end = TimeSpan.Zero;

                var first = MainWindowVM.Subtitle.AllParts.FirstOrDefault();

                if (first != null)
                    start = first.ModifiedStartTiming.TimeSpan;

                var last = MainWindowVM.Subtitle.AllParts.LastOrDefault();

                if (last != null)
                    end = last.ModifiedEndTiming.TimeSpan;

                MainWindowVM.FilterCriteria.StartTime = start;
                MainWindowVM.FilterCriteria.EndTime = end;

                MainWindowVM.FilterCriteria.FilterCommand.Execute(null);
            }
        }

        private void SaveSubtitle_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Subtitle (*.srt)|*.srt";

            bool? result = dialog.ShowDialog();

            if (result.HasValue && result.Value)
            {
                MainWindowVM.Subtitle.Save(dialog.FileName);
            }
        }

        public void ShowEditSubtitleFlyout(SubtitlePart part)
        {
            //SubtitleEditorFlyout.DataContext = part;
            SubtitleEditorFlyout.IsOpen = true;
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            FilterFlyout.IsOpen = !FilterFlyout.IsOpen;
        }
    }
}
