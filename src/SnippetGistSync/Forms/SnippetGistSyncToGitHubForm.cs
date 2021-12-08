using System;
using System.Windows.Forms;

namespace SnippetGistSync {
    public partial class SnippetGistSyncToGitHubForm : Form {
        public SnippetGistSyncToGitHubForm() {
            InitializeComponent();

            webBrowser1.ScriptErrorsSuppressed = true;

        }

        private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e) {
            if (webBrowser1.ReadyState < WebBrowserReadyState.Complete) {
                return;
            }

            if (webBrowser1.Url == new Uri("https://github.com/settings/tokens/new")) {
                MessageBox.Show($"一、[scopes]請選擇'gist'。{Environment.NewLine}二、產生token後複製到剪貼簿並關閉GitHub視窗。{Environment.NewLine}三、將token貼到'Personal access token'欄位。{Environment.NewLine}四、按'儲存'按鈕。");
            }
        }

        private void SnippetGistSyncToGitHubForm_FormClosing(object sender, FormClosingEventArgs e) {
            webBrowser1.Navigate("javascript:void((function(){var a,b,c,e,f;f=0;a=document.cookie.split('; ');for(e=0;e<a.length&&a[e];e++){f++;for(b='.'+location.host;b;b=b.replace(/^(?:%5C.|[^%5C.]+)/,'')){for(c=location.pathname;c;c=c.replace(/.$/,'')){document.cookie=(a[e]+'; domain='+b+'; path='+c+'; expires='+new Date((new Date()).getTime()-1e11).toGMTString());}}}})())"); //清除Cookie，參考來源：https://stackoverflow.com/a/1234299
        }
    }
}
