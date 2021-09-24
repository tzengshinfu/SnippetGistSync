using Microsoft;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
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
        private static IVsActivityLog log;
        private static IVsTextManager2 textManager;
        private static ShellSettingsManager settingsManager;
        private static WritableSettingsStore userSettingsStore;
        public static readonly string ThisExtensionName = "SnippetGistSync";
        private static GitHubClient gitHub;
        private static List<SnippetDirectoryPath> snippetDirectoryPaths;
        private static readonly int suspendSeconds = 5;
        private static readonly int maxErrorCounts = 5;
        private static int cumulativeErrorCounts = 0;
        /// <summary>
        /// "[已刪除]"
        /// </summary>
        private static readonly string deletedContextText = "[已刪除]";

        public static GitHubClient GitHub {
            get {
                if (gitHub == null) {
                    gitHub = new GitHubClient(new ProductHeaderValue(ThisExtensionName)) {
                        Credentials = new Credentials(UserPAT)
                    };
                }

                return gitHub;
            }
        }
        public static WritableSettingsStore UserSettingsStore {
            get {
                if (!userSettingsStore.CollectionExists(ThisExtensionName)) {
                    userSettingsStore.CreateCollection(ThisExtensionName);
                }

                return userSettingsStore;
            }
        }
        public static string UserName {
            get {
                return UserSettingsStore.GetString(ThisExtensionName, "UserName", "");
            }
            set {
                UserSettingsStore.SetString(ThisExtensionName, "UserName", value);
            }
        }
        public static string UserPAT {
            get {
                return UserSettingsStore.GetString(ThisExtensionName, "UserPAT", "");
            }
            set {
                UserSettingsStore.SetString(ThisExtensionName, "UserPAT", value);
            }
        }
        public static bool IsAutoSyncActionEnabled {
            get {
                return UserSettingsStore.GetBoolean(ThisExtensionName, "IsAutoSyncActionEnabled", false);
            }
            set {
                UserSettingsStore.SetBoolean(ThisExtensionName, "IsAutoSyncActionEnabled", value);
            }
        }
        public static string SnippetDirectoryPathJsonStr {
            get {
                return UserSettingsStore.GetString(ThisExtensionName, "SnippetDirectoryPathJsonStr", "");
            }
            set {
                UserSettingsStore.SetString(ThisExtensionName, "SnippetDirectoryPathJsonStr", value);
            }
        }
        public static List<SnippetGuid> SnippetGuids {
            get {
                return new List<SnippetGuid>() {
                    new SnippetGuid() { CodeLanguage = "csharp", DirectoryName = "Visual C#", Guid = new Guid("694DD9B6-B865-4C5B-AD85-86356E9C88DC") },
                    new SnippetGuid() { CodeLanguage = "vb", DirectoryName = "Visual Basic", Guid = new Guid("3A12D0B8-C26C-11D0-B442-00A0244A1DD2") },
                    new SnippetGuid() { CodeLanguage = "fsharp", DirectoryName = "Visual F#", Guid = new Guid("bc6dd5a5-d4d6-4dab-a00d-a51242dbaf1b") },
                    new SnippetGuid() { CodeLanguage = "cpp", DirectoryName = "Visual C++", Guid = new Guid("B2F072B0-ABC1-11D0-9D62-00C04FD9DFD9") },
                    new SnippetGuid() { CodeLanguage = "xaml", DirectoryName = "XAML", Guid = new Guid("CD53C9A1-6BC2-412B-BE36-CC715ED8DD41") },
                    new SnippetGuid() { CodeLanguage = "xml", DirectoryName = "XML", Guid = new Guid("F6819A78-A205-47B5-BE1C-675B3C7F0B8E") },
                    new SnippetGuid() { CodeLanguage = "typescript", DirectoryName = "TypeScript", Guid = new Guid("4a0dddb5-7a95-4fbf-97cc-616d07737a77") },
                    new SnippetGuid() { CodeLanguage = "python", DirectoryName = "Python", Guid = new Guid("bf96a6ce-574f-3259-98be-503a3ad636dd") },
                    new SnippetGuid() { CodeLanguage = "sql", DirectoryName = "SQL_SSDT", Guid = new Guid("ed1a9c1c-d95c-4dc1-8db8-e5a28707a864") },
                    new SnippetGuid() { CodeLanguage = "html", DirectoryName = "HTML", Guid = new Guid("58E975A0-F8FE-11D2-A6AE-00104BCC7269") },
                    new SnippetGuid() { CodeLanguage = "css", DirectoryName = "CSS", Guid = new Guid("A764E898-518D-11d2-9A89-00C04F79EFC3") },
                    new SnippetGuid() { CodeLanguage = "javascript", DirectoryName = "JavaScript", Guid = new Guid("71d61d27-9011-4b17-9469-d20f798fb5c0") },
                };
            }
        }

        internal static async Task InitializeAsync(AsyncPackage package) {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            serviceProvider = package;
            settingsManager = new ShellSettingsManager(serviceProvider);
            userSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
            log = await package.GetServiceAsync(typeof(SVsActivityLog)) as IVsActivityLog;
            Assumes.Present(log);
            textManager = await package.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager2;
            Assumes.Present(textManager);
            snippetDirectoryPaths = GetSnippetDirectoryPaths();

            snippetDirectoryPaths.ForEach(snippetDirectoryPath => {
                var snippetFileWatcher = new FileSystemWatcher(snippetDirectoryPath.DirectoryPath, "*.snippet") {
                    NotifyFilter = NotifyFilters.FileName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                snippetFileWatcher.Deleted += new FileSystemEventHandler((sender, e) => {
                    _ = Task.Run(() => { _ = OnSnippetFileDeletedAsync(sender, e); });
                });
            });
            _ = Task.Run(() => { _ = StartAutoSyncAsync(); });
        }

        private static List<SnippetDirectoryPath> GetSnippetDirectoryPaths() {
            SnippetDirectoryPathJsonStr = string.IsNullOrWhiteSpace(SnippetDirectoryPathJsonStr) ? JsonConvert.SerializeObject(GetSnippetDirectoryPathsFromVS()) : SnippetDirectoryPathJsonStr;

            return JsonConvert.DeserializeObject<List<SnippetDirectoryPath>>(SnippetDirectoryPathJsonStr);
        }

        private static List<SnippetDirectoryPath> GetSnippetDirectoryPathsFromVS() {
            var snippetDirectoryPaths = new List<SnippetDirectoryPath>();
            var expansionManager = new Func<IVsExpansionManager>(() => {
                IVsExpansionManager m_exManager = null;

                textManager.GetExpansionManager(out m_exManager);

                return m_exManager;
            })();

            foreach (var snippetGuid in SnippetGuids) {
                var expansionEnumerator = new Func<IVsExpansionEnumeration>(() => {
                    IVsExpansionEnumeration funcResult = null;

                    try {
                        expansionManager.EnumerateExpansions(snippetGuid.Guid, 0, null, 0, 1, 0, out funcResult);
                    }
                    catch {
                    }

                    return funcResult;
                })();

                if (expansionEnumerator != null) {
                    var expansionInfo = new VsExpansion();
                    var pExpansionInfo = new IntPtr[1];
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
                            expansionInfo = (VsExpansion)Marshal.PtrToStructure(pExpansionInfo[0], typeof(VsExpansion));
                            var snippetXML = XDocument.Load(expansionInfo.path);
                            var nameSpace = snippetXML.Root.Name.Namespace.NamespaceName;
                            var author = snippetXML.Descendants($"{{{nameSpace}}}Author").FirstOrDefault()?.Value ?? "";

                            //只獲取使用者自訂程式碼片段(排除Visual Studio內建片段)
                            if (author.Contains("Microsoft") || expansionInfo.path.Contains("AddaNewRowToTypedDataTable.snippet")) {
                                continue;
                            }

                            var expansionDirectoryPath = Path.GetDirectoryName(expansionInfo.path);

                            if (!snippetDirectoryPaths.Exists(snippetCodeLanguageDirectoryPath => snippetCodeLanguageDirectoryPath.DirectoryPath == expansionDirectoryPath)) {
                                snippetDirectoryPaths.Add(new SnippetDirectoryPath() { CodeLanguage = snippetGuid.CodeLanguage, DirectoryPath = expansionDirectoryPath });
                            }
                        }
                    }

                    Marshal.FreeCoTaskMem(pExpansionInfo[0]);
                }
            }

            return snippetDirectoryPaths;
        }

        private static string GetCodeLanguage(this KeyValuePair<string, GistFile> gistFile) {
            return gistFile.Key.Split('|')[0];
        }

        private static string GetFileName(this KeyValuePair<string, GistFile> gistFile) {
            return gistFile.Key.Split('|')[1];
        }

        private static DateTime GetLastUploadTimeUtc(this KeyValuePair<string, GistFile> gistFile) {
            return DateTime.Parse(gistFile.Key.Split('|')[2]).ToUniversalTime();
        }

        internal static async Task StartAutoSyncAsync() {
            while (true) {
                if (!IsAutoSyncActionEnabled) {
                    await Task.Delay(suspendSeconds * 1000);

                    continue;
                }

                try {
                    var snippetFiles = GetSnippetFiles();
                    var snippetFilesLastWriteTimeUtc = GetSnippetFilesLastWriteTimeUtc(snippetFiles);
                    var snippetGist = await GetSnippetGistAsync();
                    var snippetGistLastUploadTimeUtc = GetSnippetGistLastUploadTimeUtc(snippetGist);

                    #region 新增GitHub Gist [SnippetGistSync]
                    if (snippetGist == null) {
                        LogInfomation("同步開始");

                        var newSnippetGist = new NewGist() { Public = false, Description = $"Visual Studio 延伸模組 [{ThisExtensionName}] 已同步程式碼片段檔案儲存於此。" };

                        foreach (var localFile in snippetFiles) {
                            LogInfomation($"上傳片段[{localFile.CodeLanguage + "|" + localFile.FileName}]->GitHub Gist [{ThisExtensionName}]");

                            var newGistFileName = localFile.CodeLanguage + "|" + localFile.FileName + "|" + localFile.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

                            newSnippetGist.Files.Add(newGistFileName, localFile.FileCotent);
                        }
                        newSnippetGist.Files.Add(ThisExtensionName, $@"{{""lastUploadTimeUtc"":""{snippetFilesLastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}""}}");

                        if (newSnippetGist.Files.Count > 0) {
                            await GitHub.Gist.Create(newSnippetGist);
                        }

                        LogInfomation("同步完成");

                        await Task.Delay(suspendSeconds * 1000);

                        continue;
                    }
                    #endregion

                    #region 不需同步
                    if (snippetFilesLastWriteTimeUtc == snippetGistLastUploadTimeUtc) {
                        LogInfomation("同步開始");
                        LogInfomation("不需同步");
                        LogInfomation("同步完成");

                        await Task.Delay(suspendSeconds * 1000);

                        continue;
                    }
                    #endregion

                    #region 同步本機端／遠端                    
                    {
                        LogInfomation("同步開始");

                        var updateSnippetGist = new GistUpdate();

                        foreach (var localFile in snippetFiles) {
                            var gistNameByLocal = localFile.CodeLanguage + "|" + localFile.FileName + "|" + localFile.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
                            var matchedGistFile = snippetGist.Files.ToList().FirstOrDefault(gistFile => gistFile.GetCodeLanguage() == localFile.CodeLanguage && gistFile.GetFileName() == localFile.FileName);

                            //本機端存在，但遠端不存在，則上傳
                            if (matchedGistFile.Key == null) {
                                LogInfomation($"上傳片段[{localFile.CodeLanguage + "|" + localFile.FileName}]->GitHub Gist [{ThisExtensionName}]");

                                updateSnippetGist.Files.Add(gistNameByLocal, new GistFileUpdate() { NewFileName = gistNameByLocal, Content = localFile.FileCotent });
                            }
                            //本機端存在，且遠端亦存在
                            else {
                                var gistFileLastUploadTimeUtc = matchedGistFile.GetLastUploadTimeUtc();

                                //若本機端較新，則更新遠端
                                if (localFile.LastWriteTimeUtc > gistFileLastUploadTimeUtc) {
                                    LogInfomation($"更新片段[{localFile.CodeLanguage + "|" + localFile.FileName}]->GitHub Gist [{ThisExtensionName}]");

                                    updateSnippetGist.Files.Add(matchedGistFile.Key, new GistFileUpdate() { NewFileName = gistNameByLocal, Content = localFile.FileCotent });
                                }
                                //若遠端較新，則更新本機端                                
                                else if (localFile.LastWriteTimeUtc < gistFileLastUploadTimeUtc) {
                                    LogInfomation($"更新片段[{localFile.CodeLanguage + "|" + localFile.FileName}]->本機");

                                    File.WriteAllText(localFile.FilePath, matchedGistFile.Value.Content);
                                    File.SetLastWriteTimeUtc(localFile.FilePath, gistFileLastUploadTimeUtc);
                                }
                            }
                        }

                        foreach (var gistFile in snippetGist.Files.ToList()) {
                            //跳過儲存上傳時間的Gist file
                            if (gistFile.Key == ThisExtensionName) {
                                continue;
                            }

                            var matchedLocalFile = snippetFiles.FirstOrDefault(localFile => localFile.CodeLanguage == gistFile.GetCodeLanguage() && localFile.FileName == gistFile.GetFileName());

                            //本機端不存在，但遠端存在，則下載
                            if (matchedLocalFile == null) {
                                if (gistFile.Value.Content == deletedContextText) {
                                    continue;
                                }

                                LogInfomation($"下載片段[{matchedLocalFile.CodeLanguage + "|" + matchedLocalFile.FileName}]->本機");

                                var localDirectoryPath = snippetDirectoryPaths.First(snippetDirectoryPath => snippetDirectoryPath.CodeLanguage == gistFile.GetCodeLanguage()).DirectoryPath;
                                var localFilePath = localDirectoryPath + "\\" + gistFile.GetFileName();
                                var localFileLastWriteTimeUtc = gistFile.GetLastUploadTimeUtc();

                                File.WriteAllText(localFilePath, gistFile.Value.Content);
                                File.SetLastWriteTimeUtc(localFilePath, localFileLastWriteTimeUtc);
                            }
                            //遠端內容為"[已刪除]"字樣，則刪除本機端
                            else {
                                if (gistFile.Value.Content != deletedContextText) {
                                    continue;
                                }

                                if (matchedLocalFile.LastWriteTimeUtc > gistFile.GetLastUploadTimeUtc()) {
                                    continue;
                                }

                                LogInfomation($"刪除片段[{matchedLocalFile.CodeLanguage + "|" + matchedLocalFile.FileName}]->本機");

                                File.Delete(matchedLocalFile.FilePath);
                            }
                        }

                        if (updateSnippetGist.Files.Count > 0) {
                            await GitHub.Gist.Edit(snippetGist.Id, updateSnippetGist);
                        }

                        LogInfomation("同步完成");

                        await Task.Delay(suspendSeconds * 1000);

                        continue;
                    }
                    #endregion
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

        private static async Task OnSnippetFileDeletedAsync(object sender, FileSystemEventArgs e) {
            var snippetGist = await GetSnippetGistAsync();
            var updateSnippetGist = new GistUpdate();
            var localFileCodeLanguage = SnippetGuids.First(snippetGuid => e.FullPath.Contains(snippetGuid.DirectoryName)).CodeLanguage;
            var localFileName = Path.GetFileName(e.FullPath);
            var matchedGistFile = snippetGist.Files.ToList().FirstOrDefault(gistFile => gistFile.GetCodeLanguage() == localFileCodeLanguage && gistFile.GetFileName() == localFileName);

            if (matchedGistFile.Key != null) {
                LogInfomation($"刪除片段[{localFileCodeLanguage + "|" + localFileName}]->GitHub Gist [{ThisExtensionName}]");

                //將遠端內容改為"[已刪除]"字樣
                updateSnippetGist.Files.Add(matchedGistFile.Key, new GistFileUpdate() { NewFileName = localFileCodeLanguage + "|" + localFileName + "|" + DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"), Content = deletedContextText });
            }

            if (updateSnippetGist.Files.Count > 0) {
                await GitHub.Gist.Edit(snippetGist.Id, updateSnippetGist);
            }

            LogInfomation("同步完成");
        }

        public static List<SnippetFile> GetSnippetFiles() {
            var snippetFiles = new List<SnippetFile>();

            foreach (var snippetGuid in SnippetGuids) {
                var snippetDirectoryByCodeLanguagePaths = snippetDirectoryPaths.Where(snippetDirectoryPath => snippetDirectoryPath.CodeLanguage == snippetGuid.CodeLanguage).ToList();

                foreach (var snippetDirectoryByCodeLanguagePath in snippetDirectoryByCodeLanguagePaths) {
                    var snippetFilePaths = Directory.EnumerateFiles(snippetDirectoryByCodeLanguagePath.DirectoryPath, "*.snippet");

                    foreach (var snippetFilePath in snippetFilePaths.ToList()) {
                        var snippetFileInfo = new SnippetFile() {
                            FileName = Path.GetFileName(snippetFilePath),
                            LastWriteTimeUtc = File.GetLastWriteTimeUtc(snippetFilePath),
                            FilePath = snippetFilePath,
                            FileCotent = File.ReadAllText(snippetFilePath),
                            CodeLanguage = snippetGuid.CodeLanguage
                        };

                        snippetFiles.Add(snippetFileInfo);
                    }
                }
            }

            return snippetFiles;
        }

        public static DateTime GetSnippetFilesLastWriteTimeUtc(List<SnippetFile> snippetFiles) {
            return snippetFiles.Select(localFile => localFile.LastWriteTimeUtc).DefaultIfEmpty(DateTime.MinValue).Max();
        }

        public static async Task<Gist> GetSnippetGistAsync() {
            var snippetGist = (await GitHub.Gist.GetAllForUser(UserName)).FirstOrDefault(gist => gist.Public == false && gist.Files.TryGetValue(ThisExtensionName, out _));

            if (snippetGist != null) {
                //Gist.GetAllForUser回傳結果的Content屬性為null，必須用Gist.Get再取得一次才會有值。
                return await GitHub.Gist.Get(snippetGist.Id);
            }
            else {
                return snippetGist;
            }
        }

        public static DateTime GetSnippetGistLastUploadTimeUtc(Gist snippetGist) {
            if (snippetGist == null) {
                return DateTime.MinValue;
            }
            else {
                var gistContent = snippetGist.Files.Where(file => file.Key == ThisExtensionName).Select(file => file.Value).Single().Content;
                var gistJSON = JsonConvert.DeserializeAnonymousType(gistContent, new { lastUploadTimeUtc = "" });
                var lastUploadTimeUtc = DateTime.Parse(gistJSON.lastUploadTimeUtc).ToUniversalTime();

                return lastUploadTimeUtc;
            }
        }

        public static void LogInfomation(string message) {
            log.LogEntry((UInt32)__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION, ThisExtensionName, message);
        }

        public static void LogError(string message) {
            log.LogEntry((UInt32)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR, ThisExtensionName, message);
        }

        public static void ResetCumulativeErrorCounts() {
            cumulativeErrorCounts = 0;
        }

        public static void ResetSnippetDirectoryPaths() {
            SnippetDirectoryPathJsonStr = "";
            snippetDirectoryPaths = GetSnippetDirectoryPaths();
        }
    }

    public class SnippetFile {
        public string CodeLanguage { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public string FileCotent { get; set; }
    }

    public class SnippetDirectoryPath {
        public string CodeLanguage { get; set; }
        public string DirectoryPath { get; set; }
    }

    public class SnippetGuid {
        public string CodeLanguage { get; set; }
        public string DirectoryName { get; set; }
        public Guid Guid { get; set; }
    }
}
