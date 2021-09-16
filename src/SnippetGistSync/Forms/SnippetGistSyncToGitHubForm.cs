using System;
using System.Windows.Forms;

namespace SnippetGistSync {
    public partial class SnippetGistSyncToGitHubForm : Form {
        public SnippetGistSyncToGitHubForm() {
            InitializeComponent();
        }

        private void webView21_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e) {
            if (webView21.Source == new Uri("https://github.com/settings/tokens/new")) {
                MessageBox.Show("Step1:Login");
            }
        }

        private void SnippetGistSyncToGitHubForm_FormClosing(object sender, FormClosingEventArgs e) {
            webView21.CoreWebView2.CookieManager.DeleteAllCookies();
        }
    }
}
