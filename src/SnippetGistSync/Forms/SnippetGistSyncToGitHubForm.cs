using System;
using System.Windows.Forms;

namespace SnippetGistSync {
    public partial class SnippetGistSyncToGitHubForm : Form {
        public SnippetGistSyncToGitHubForm() {
            InitializeComponent();
        }

        private void webView21_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e) {
            if (webView21.Source == new Uri("https://github.com/settings/tokens/new")) {
                MessageBox.Show($"一、[scopes]請選擇'gist'。{Environment.NewLine}二、產生token後複製到剪貼簿並關閉GitHub視窗。{Environment.NewLine}三、將token貼到'Personal access token'欄位。{Environment.NewLine}四、按'儲存'按鈕。");
            }
        }

        private void SnippetGistSyncToGitHubForm_FormClosing(object sender, FormClosingEventArgs e) {
            webView21.CoreWebView2.CookieManager.DeleteAllCookies();
        }
    }
}
