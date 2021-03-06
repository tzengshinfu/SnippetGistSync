using ReactiveUI;
using System.Drawing;
using System.Reactive;
using System.Reactive.Linq;
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
                a(this.BindCommand(ViewModel, vm => vm.Save, v => v.btnSave));
                a(this.BindCommand(ViewModel, vm => vm.ToGitHub, v => v.btnToGitHub));
                a(this.BindCommand(ViewModel, vm => vm.ToggleAutoSync, v => v.btnToggleAutoSync));
                a(this.BindCommand(ViewModel, vm => vm.ResetPathCache, v => v.btnResetPathCache));
            });

            ViewModel = new SnippetGistSyncOptionWindowViewModel();                
            ViewModel.WhenAnyValue(vm => vm.UserName, vm => vm.UserPAT).Select(vm => !string.IsNullOrWhiteSpace(vm.Item1) && !string.IsNullOrWhiteSpace(vm.Item2) ? true : false).BindTo(this, vm => vm.btnSave.Enabled);
            ViewModel.WhenAnyValue(vm => vm.UserName, vm => vm.UserPAT).Select(vm => !string.IsNullOrWhiteSpace(vm.Item1) && !string.IsNullOrWhiteSpace(vm.Item2) ? true : false).BindTo(this, vm => vm.btnToggleAutoSync.Enabled);
            ViewModel.WhenAnyValue(vm => vm.UserName, vm => vm.UserPAT).Select(vm => !string.IsNullOrWhiteSpace(vm.Item1) && !string.IsNullOrWhiteSpace(vm.Item2) ? true : false).BindTo(this, vm => vm.btnResetPathCache.Enabled);
            ViewModel.WhenAnyValue(vm => vm.IsAutoSyncActionEnabled).Select(vm => vm ? "已啟用同步" : "已停用同步").BindTo(this, vm => vm.btnToggleAutoSync.Text);
            ViewModel.WhenAnyValue(vm => vm.IsAutoSyncActionEnabled).Select(vm => vm ? Color.DarkGreen : Color.DarkRed).BindTo(this, vm => vm.btnToggleAutoSync.ForeColor);
        }        
    }

    public class SnippetGistSyncOptionWindowViewModel : ReactiveObject {
        private string userName;
        private string userPAT;
        private bool isAutoSyncActionEnabled;
        public ReactiveCommand<Unit, Unit> Save;
        public ReactiveCommand<Unit, Unit> ToGitHub;
        public ReactiveCommand<Unit, Unit> ToggleAutoSync;
        public ReactiveCommand<Unit, Unit> ResetPathCache;

        public SnippetGistSyncOptionWindowViewModel() {
            UserName = SnippetGistSyncService.UserName;
            UserPAT = SnippetGistSyncService.UserPAT;
            IsAutoSyncActionEnabled = SnippetGistSyncService.IsAutoSyncActionEnabled;
            Save = ReactiveCommand.Create(() => {
                SnippetGistSyncService.UserName = UserName;
                SnippetGistSyncService.UserPAT = UserPAT;
                MessageBox.Show("儲存完成");
            });
            ToGitHub = ReactiveCommand.Create(() => {
                var snippetGistSyncToGitHubForm = new SnippetGistSyncToGitHubForm();
                snippetGistSyncToGitHubForm.ShowDialog();
            });
            ToggleAutoSync = ReactiveCommand.Create(() => {
                IsAutoSyncActionEnabled = !IsAutoSyncActionEnabled;                

                if (IsAutoSyncActionEnabled) {
                    SnippetGistSyncService.ResetCumulativeErrorCounts();
                }

                SnippetGistSyncService.IsAutoSyncActionEnabled = IsAutoSyncActionEnabled;
            });
            ResetPathCache = ReactiveCommand.Create(() => {
                SnippetGistSyncService.ResetSnippetDirectoryPaths();
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
    }
}
