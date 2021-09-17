using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.TextManager.Interop;
using Newtonsoft.Json;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Task = System.Threading.Tasks.Task;

namespace SnippetGistSync {
    static class SnippetGistSyncService {
        private static AsyncPackage serviceProvider;
        private static EnvDTE.DTE dte;
        private static IVsActivityLog log;
        private static IVsTextManager2 textManager;
        private static ShellSettingsManager settingsManager;
        private static WritableSettingsStore userSettingsStore;
        public static readonly string ExtensionName = "SnippetGistSync";
        private static GitHubClient gitHub;
        private static readonly int suspendSeconds = 10;
        private static readonly int maxErrorCounts = 10;
        public static GitHubClient GitHub {
            get {
                if (gitHub == null) {
                    gitHub = new GitHubClient(new ProductHeaderValue(ExtensionName)) {
                        Credentials = new Credentials(UserPAT)
                    };
                }

                return gitHub;
            }
        }
        public static string UserName {
            get {
                if (!userSettingsStore.CollectionExists(ExtensionName)) {
                    userSettingsStore.CreateCollection(ExtensionName);
                }

                return userSettingsStore.GetString(ExtensionName, "UserName", "");
            }
            set {
                if (!userSettingsStore.CollectionExists(ExtensionName)) {
                    userSettingsStore.CreateCollection(ExtensionName);
                }

                userSettingsStore.SetString(ExtensionName, "UserName", value);
            }
        }
        public static string UserPAT {
            get {
                if (!userSettingsStore.CollectionExists(ExtensionName)) {
                    userSettingsStore.CreateCollection(ExtensionName);
                }

                return userSettingsStore.GetString(ExtensionName, "UserPAT", "");
            }
            set {
                if (!userSettingsStore.CollectionExists(ExtensionName)) {
                    userSettingsStore.CreateCollection(ExtensionName);
                }

                userSettingsStore.SetString(ExtensionName, "UserPAT", value);
            }
        }
        public static bool IsAutoSyncActionEnabled {
            get {
                if (!userSettingsStore.CollectionExists(ExtensionName)) {
                    userSettingsStore.CreateCollection(ExtensionName);
                }

                return userSettingsStore.GetBoolean(ExtensionName, "IsAutoSyncActionEnabled", false);
            }
            set {
                if (!userSettingsStore.CollectionExists(ExtensionName)) {
                    userSettingsStore.CreateCollection(ExtensionName);
                }

                userSettingsStore.SetBoolean(ExtensionName, "IsAutoSyncActionEnabled", value);
            }
        }

        internal static async Task InitializeAsync(AsyncPackage package) {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            serviceProvider = package;
            settingsManager = new ShellSettingsManager(serviceProvider);
            userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
            dte = await package.GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            log = await package.GetServiceAsync(typeof(SVsActivityLog)) as IVsActivityLog;
            textManager = await package.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager2;

            await StartAutoSyncAsync();
        }

        internal static async Task StartAutoSyncAsync() {            
            while (true) {
                if (!IsAutoSyncActionEnabled) {
                    await Task.Delay(suspendSeconds * 1000);

                    continue;
                }

                var cumulativeErrorCounts = 0;

                try {
                    var snippetLocalFiles = GetSnippetLocalFiles();
                    var snippetLocalFilesLastWriteTimeUtc = GetSnippetLocalFilesLastWriteTimeUtc(snippetLocalFiles);
                    var snippetGist = await GetSnippetGistAsync();
                    var snippetGistLastUploadTimeUtc = GetSnippetGistLastUploadTimeUtc(snippetGist);

                    #region 新增SnippetSyncerGist
                    if (snippetGist == null) {
                        LogInfomation("同步開始");

                        var newSnippetGist = new NewGist() { Public = false, Description = $"Visual Studio 延伸模組 [{ExtensionName}] 已同步程式碼片段檔案儲存於此。" };

                        snippetLocalFiles.ForEach((snippetFile) => {
                            var snippetGistFileName = snippetFile.CodeLanguage + "|" + snippetFile.FileName + "|" + snippetFile.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

                            LogInfomation($"新增檔案[{snippetFile.FilePath}]");

                            newSnippetGist.Files.Add(snippetGistFileName, snippetFile.FileCotent);
                        });

                        newSnippetGist.Files.Add(ExtensionName, $@"{{""lastUploadTimeUtc"":""{snippetLocalFilesLastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}""}}");
                        await GitHub.Gist.Create(newSnippetGist);

                        LogInfomation("同步完成");

                        await Task.Delay(suspendSeconds * 1000);

                        continue;
                    }
                    #endregion

                    #region 不需同步
                    if (snippetLocalFilesLastWriteTimeUtc == snippetGistLastUploadTimeUtc) {
                        LogInfomation("同步開始");
                        LogInfomation("不需同步");
                        LogInfomation("同步完成");

                        await Task.Delay(suspendSeconds * 1000);

                        continue;
                    }
                    #endregion

                    #region 上傳本機端
                    if (snippetLocalFilesLastWriteTimeUtc > snippetGistLastUploadTimeUtc) {
                        LogInfomation("同步開始");

                        var updateSnippetGist = new GistUpdate();

                        snippetGist.Files.ToList().ForEach((snippetGistFile) => {
                            var snippetGistFileCodeLanguage = snippetGistFile.Key.Split('|')[0];
                            var snippetGistFileName = snippetGistFile.Key.Split('|')[1];                            
                            var matchSnippetLocalFile = snippetLocalFiles.Where(snippetFile => snippetFile.CodeLanguage == snippetGistFileCodeLanguage && snippetFile.FileName == snippetGistFileName).SingleOrDefault();

                            if (matchSnippetLocalFile != null) {
                                var snippetGistFileLastWriteTimeUtc = DateTime.Parse(snippetGistFile.Key.Split('|')[2]).ToUniversalTime();
                                var snippetLocalFileLastWriteTimeUtc = matchSnippetLocalFile.LastWriteTimeUtc;

                                if (snippetLocalFileLastWriteTimeUtc > snippetGistFileLastWriteTimeUtc) { 
                                    updateSnippetGist.Files.Add(snippetGistFile.Key, new GistFileUpdate() { });                                
                                }
                            }                            
                        });

                        await GitHub.Gist.Edit(snippetGist.Id, updateSnippetGist);
                        //更新SnippetSyncerGist                    
                        //尋找符合的Gist File
                        if (true) {
                            //更新內容
                        }
                        else {
                            //刪除不符合的Gist File
                        }
                
                        LogInfomation("同步完成");

                        await Task.Delay(suspendSeconds * 1000);

                        continue;
                    }
                    #endregion

                    #region 下載遠端
                    //if (snippetFileLastWriteTimeUtc < snippetGistLastUploadTimeUtc) {
                    //    LogInfomation("同步開始");
                    //    //更新機地端所有Snippet
                    //    //刪除不符合的Snippet File
                    //
                    //    LogInfomation("同步完成");

                    //    await Task.Delay(suspendSecond * 1000);

                    //    continue;
                    //}
                    #endregion

                    //var gg = github.Gist.Get(SnippetGistSyncGist.Id).GetAwaiter().GetResult();
                    //var time = gg.Files.Where(f => f.Key == "SnippetGistSync").Select(f => f.Value).Single();
                    //var c = time.Content;
                    //var account = JsonConvert.DeserializeAnonymousType(c, new { lastUpload = "" });
                    //var a = DateTime.Parse(account.lastUpload);
                    //
                    //
                    //var updateGist = new GistUpdate() { };
                    //
                    //updateGist.Files.Add("csharp|tryi.snippet", new GistFileUpdate() { NewFileName = "csharp|tryi.snippet", Content = @"
                    //<?xml version=""1.0"" encoding=""utf-8""?>
                    //<CodeSnippets xmlns=""http://schemas.microsoft.com/VisualStudio/2005/CodeSnippet"">
                    //  <CodeSnippet Format=""1.0.0"">
                    //    <Header>
                    //      <SnippetTypes>
                    //        <SnippetType>Expansion</SnippetType>
                    //      </SnippetTypes>
                    //      <Title>appset</Title>
                    //      <Author>tzengshinfu</Author>
                    //      <Description>取得目前應用程式預設組態的 AppSettingsSection 資料</Description>
                    //      <HelpUrl>
                    //      </HelpUrl>
                    //      <Shortcut>appset</Shortcut>
                    //    </Header>
                    //    <Snippet>
                    //      <Declarations>
                    //        <Literal Editable=""true"">
                    //          <ID>key</ID>
                    //          <ToolTip>索引鍵名稱</ToolTip>
                    //          <Default>key</Default>
                    //          <Function>
                    //          </Function>
                    //        </Literal>
                    //      </Declarations>
                    //      <Code Language=""csharp"" Delimiter=""$""><![CDATA[ConfigurationManager.AppSettings[""$key$""]]]></Code>
                    //    </Snippet>
                    //  </CodeSnippet>
                    //</CodeSnippets>
                    //" });
                    //github.Gist.Edit(SnippetGistSyncGist.Id, updateGist);                

                    //LogInfomation("同步完成");
                    //await Task.Delay(suspendSecond * 1000);
                    //continue;
                }
                catch (Exception ex) {
                    cumulativeErrorCounts++;

                    LogError(ex.ToString());

                    if (cumulativeErrorCounts >= maxErrorCounts) {
                        LogError($"錯誤次數已達{maxErrorCounts}次，同步中止");

                        IsAutoSyncActionEnabled = false;
                    }

                    await Task.Delay(suspendSeconds * 1000);

                    continue;
                }
            }
        }

        public static List<SnippetLocalFile> GetSnippetLocalFiles() {
            var snippetFileInfoList = new List<SnippetLocalFile>();
            var expansionsList = new List<VsExpansion>();
            var expansionManager = new Func<IVsExpansionManager>(() => {
                IVsExpansionManager m_exManager = null;

                textManager.GetExpansionManager(out m_exManager);

                return m_exManager;
            })();

            SnippetInfoMap.List.ForEach((snippetInfo) => {
                var expansionEnumerator = new Func<IVsExpansionEnumeration>(() => {
                    IVsExpansionEnumeration funcResult = null;

                    try {
                        expansionManager.EnumerateExpansions(snippetInfo.Guid,
                        0,     // return all info
                        null,    // return all types
                        0,     // return all types
                        1,     // include snippets without types
                        0,     // do not include duplicates
                        out funcResult);
                    }
                    catch {
                    }

                    return funcResult;
                })();

                if (expansionEnumerator != null) {
                    // Cache our expansions in a VsExpansion array
                    var expansionInfo = new VsExpansion();
                    var pExpansionInfo = new IntPtr[1];

                    // Allocate enough memory for one VSExpansion structure. This memory is filled in by the Next method.
                    pExpansionInfo[0] = Marshal.AllocCoTaskMem(Marshal.SizeOf(expansionInfo));

                    var count = new Func<uint>(() => {
                        uint funcResult = 0;

                        expansionEnumerator.GetCount(out funcResult);

                        return funcResult;
                    })();

                    for (uint i = 0; i < count; i++) {
                        var fetched = new Func<uint>(() => {
                            uint funcResult = 0;

                            expansionEnumerator.Next(1, pExpansionInfo, out funcResult);

                            return funcResult;
                        })();

                        if (fetched > 0) {
                            // Convert the returned blob of data into a structure that can be read in managed code.
                            expansionInfo = (VsExpansion)Marshal.PtrToStructure(pExpansionInfo[0], typeof(VsExpansion));

                            var snippetXml = XDocument.Load(expansionInfo.path);
                            var nameSpace = snippetXml.Root.Name.Namespace.NamespaceName;
                            var author = snippetXml.Descendants($"{{{nameSpace}}}Author").FirstOrDefault()?.Value ?? "";                            

                            if (author.Contains("Microsoft") || expansionInfo.path.Contains("AddaNewRowToTypedDataTable.snippet")) {
                                continue;
                            }

                            var language = snippetXml.Descendants($"{{{nameSpace}}}Code").FirstOrDefault()?.Attribute("Language")?.Value.ToLower() ?? "";

                            var snippetFileInfo = new SnippetLocalFile() {
                                FileName = Path.GetFileName(expansionInfo.path),
                                LastWriteTimeUtc = File.GetLastWriteTimeUtc(expansionInfo.path),
                                FilePath = expansionInfo.path,
                                FileCotent = File.ReadAllText(expansionInfo.path),
                                CodeLanguage = language
                            };

                            snippetFileInfoList.Add(snippetFileInfo);
                        }
                    }

                    Marshal.FreeCoTaskMem(pExpansionInfo[0]);
                }
            });

            return snippetFileInfoList;
        }

        public static DateTime GetSnippetLocalFilesLastWriteTimeUtc(List<SnippetLocalFile> snippetFileInfoList) {
            return snippetFileInfoList.Select(snippetFileInfo => snippetFileInfo.LastWriteTimeUtc).DefaultIfEmpty(DateTime.MinValue).Max();
        }

        public static async Task<Gist> GetSnippetGistAsync() {
            var snippetSyncerGist = (await GitHub.Gist.GetAllForUser(UserName)).Where(gist=>gist.Public == false && gist.Files.TryGetValue(ExtensionName, out _)).SingleOrDefault();

            if (snippetSyncerGist != null) {                            
                //Gist.GetAllForUser回傳結果的Content屬性為null，必須用Gist.Get再取得一次才會有值。
                return await GitHub.Gist.Get(snippetSyncerGist.Id);
            }
            else {
                return snippetSyncerGist;
            }
        }

        public static DateTime GetSnippetGistLastUploadTimeUtc(Gist snippetSyncerGist) {
            if (snippetSyncerGist == null) {
                return DateTime.MinValue;
            }
            else {
                var gistContent = snippetSyncerGist.Files.Where(file => file.Key == ExtensionName).Select(file => file.Value).Single().Content;
                var gistJSON = JsonConvert.DeserializeAnonymousType(gistContent, new { lastUploadTimeUtc = "" });
                var lastUploadTimeUtc = DateTime.Parse(gistJSON.lastUploadTimeUtc).ToUniversalTime();
            
                return lastUploadTimeUtc;
            }
        }

        public static void LogInfomation(string message) {
            //ThreadHelper.ThrowIfNotOnUIThread();

            log.LogEntry((UInt32)__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION, ExtensionName, message);
        }

        public static void LogWarning(string message) {
            //ThreadHelper.ThrowIfNotOnUIThread();

            log.LogEntry((UInt32)__ACTIVITYLOG_ENTRYTYPE.ALE_WARNING, ExtensionName, message);
        }

        public static void LogError(string message) {
            //ThreadHelper.ThrowIfNotOnUIThread();

            log.LogEntry((UInt32)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR, ExtensionName, message);
        }
    }

    class SnippetLocalFile {
        public string FileName { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public string FilePath { get; set; }
        public string FileCotent { get; set; }

        public string CodeLanguage { get; set; }
    }
}
