using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using ReactiveUI;

namespace Numerge.Gui.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public Window Window { get; set; }
        private string _pathToDirWithPack = "path to directory with packages (*nupkg)";
        private string _pathToOutputDir = "path to output directory";
        private string _pathToConfig = "path to config file";
        private string _confContent;
        public ReactiveCommand<Unit, Unit> RunMergeCommand { get; }

        public MainWindowViewModel()
        {
            RunMergeCommand = ReactiveCommand.CreateFromTask(Run);
            RunMergeCommand.ThrownExceptions.Subscribe(async ex =>
                await MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow("", ex.Message)
                    .ShowDialog(Window));
        }

        public string PathToDirWithPack
        {
            get => _pathToDirWithPack;
            set => this.RaiseAndSetIfChanged(ref _pathToDirWithPack, value);
        }

        public string PathToOutputDir
        {
            get => _pathToOutputDir;
            set => this.RaiseAndSetIfChanged(ref _pathToOutputDir, value);
        }

        public string PathToConfig
        {
            get => _pathToConfig;
            set => this.RaiseAndSetIfChanged(ref _pathToConfig, value);
        }

        public string ConfContent
        {
            get => _confContent;
            set => this.RaiseAndSetIfChanged(ref _confContent, value);
        }

        public async Task SetConfig()
        {
            var dialog = new OpenFileDialog();
            string[] result = null;
            result = await dialog.ShowAsync(Window);
            if (result != null)
            {
                PathToConfig = result.First();
                ConfContent = File.ReadAllText(result.First());
            }
        }

        public async Task SetPckgDir()
        {
            var dialog = new OpenFolderDialog();
            string result = null;
            result = await dialog.ShowAsync(Window);
            if (result != null)
            {
                PathToDirWithPack = result;
            }
        }

        public async Task SetResDir()
        {
            var dialog = new OpenFolderDialog();
            string result = null;
            result = await dialog.ShowAsync(Window);
            if (result != null)
            {
                PathToOutputDir = result;
            }
        }

        private async Task Run()
        {
            await Task.Run(() =>
            {
                NugetPackageMerger.Merge(_pathToDirWithPack, _pathToOutputDir,
                    MergeConfiguration.LoadFile(_pathToConfig), new NumergeConsoleLogger());
            });
            await MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow("", "merged").ShowDialog(Window);
        }
    }
}