using Octokit;
using ReactiveUI;
using System.Drawing;
using System.Reactive;
using System.Windows.Forms;

namespace SnippetGistSync {
    public partial class SnippetGistSyncOptionForm : Form, IViewFor<SnippetGistSyncOptionWindowViewModel> {
        public SnippetGistSyncOptionWindowViewModel ViewModel { get; set; }
        object IViewFor.ViewModel { get => ViewModel; set => ViewModel = (SnippetGistSyncOptionWindowViewModel)value; }
        public SnippetGistSyncOptionForm() {
            InitializeComponent();
            this.WhenActivated(a => {
                a(this.Bind(ViewModel, vm => vm.UserName, v => v.txtUserName.Text));
                a(this.Bind(ViewModel, vm => vm.UserPAT, v => v.txtUserPAT.Text));
                a(this.Bind(ViewModel, vm => vm.IsAutoSyncButtonEnabled, v => v.btnToggleAutoSync.Enabled));
                a(this.Bind(ViewModel, vm => vm.AutoSyncButtonText, v => v.btnToggleAutoSync.Text));
                a(this.Bind(ViewModel, vm => vm.AutoSyncButtonTextColor, v => v.btnToggleAutoSync.ForeColor));
                a(this.BindCommand(ViewModel, vm => vm.Save, v => v.btnSave));
                a(this.BindCommand(ViewModel, vm => vm.ToGitHub, v => v.btnToGitHub));
                a(this.BindCommand(ViewModel, vm => vm.ToggleAutoSync, v => v.btnToggleAutoSync));
                a(this.BindCommand(ViewModel, vm => vm.DownloadAll, v => v.btnDownloadAll));
                a(this.BindCommand(ViewModel, vm => vm.UploadAll, v => v.btnUploadAll));
            });

            ViewModel = new SnippetGistSyncOptionWindowViewModel();
        }        
    }

    public class SnippetGistSyncOptionWindowViewModel : ReactiveObject {
        private string userName;
        private string userPAT;
        private bool isAutoSyncActionEnabled;
        public ReactiveCommand<Unit, Unit> Save;
        public ReactiveCommand<Unit, Unit> ToGitHub;
        public ReactiveCommand<Unit, Unit> ToggleAutoSync;
        public ReactiveCommand<Unit, Unit> DownloadAll;
        public ReactiveCommand<Unit, Unit> UploadAll;

        public SnippetGistSyncOptionWindowViewModel() {
            UserName = SnippetGistSyncService.UserName;
            UserPAT = SnippetGistSyncService.UserPAT;
            IsAutoSyncActionEnabled = SnippetGistSyncService.IsAutoSyncActionEnabled;
            Save = ReactiveCommand.Create(() => {
                SnippetGistSyncService.UserName = UserName;
                SnippetGistSyncService.UserPAT = UserPAT;
            });
            ToGitHub = ReactiveCommand.Create(() => {
                var snippetGistSyncToGitHubForm = new SnippetGistSyncToGitHubForm();
                snippetGistSyncToGitHubForm.ShowDialog();
            });
            ToggleAutoSync = ReactiveCommand.Create(() => {
                IsAutoSyncActionEnabled = !IsAutoSyncActionEnabled;
            });
            DownloadAll = ReactiveCommand.Create(() => {

            });
            UploadAll = ReactiveCommand.Create(() => {

            });
        }

        public string UserName {
            get => userName;
            set => this.RaiseAndSetIfChanged(ref userName, value);
        }
        public string UserPAT {
            get => userPAT;
            set => this.RaiseAndSetIfChanged(ref userPAT, value);
        }
        public bool IsAutoSyncActionEnabled {
            get => isAutoSyncActionEnabled;
            set => this.RaiseAndSetIfChanged(ref isAutoSyncActionEnabled, value);
        }
        public bool IsAutoSyncButtonEnabled {
            get => (!string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(UserPAT));
        }
        public string AutoSyncButtonText {
            get => IsAutoSyncActionEnabled? "已啟用同步" : "已停用同步";
        }
        public Color AutoSyncButtonTextColor { 
            get => IsAutoSyncActionEnabled? Color.Green : Color.Gray;
        }
    }
}
