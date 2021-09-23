using Microsoft;
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
        private static IVsActivityLog log;
        private static IVsTextManager2 textManager;
        private static ShellSettingsManager settingsManager;
        private static WritableSettingsStore userSettingsStore;
        public static readonly string ThisExtensionName = "SnippetGistSync";
        private static GitHubClient gitHub;
        private static readonly int suspendSeconds = 5;
        private static readonly int maxErrorCounts = 5;
        private static int cumulativeErrorCounts = 0;
        /// <summary>
        /// "[已刪除]"
        /// </summary>
        private static readonly string deletedContextText = "[已刪除]";
        private static List<SnippetDirectoryPath> snippetDirectoryPaths;

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
        public static WritableSettingsStore UserSettingsStore {
            get {
                if (!userSettingsStore.CollectionExists(ThisExtensionName)) {
                    userSettingsStore.CreateCollection(ThisExtensionName);
                }

                return userSettingsStore;
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
            snippetDirectoryPaths = getSnippetDirectoryPaths();
            snippetDirectoryPaths.ForEach(snippetDirectoryPath => {
                var snippetFileWatcher = new FileSystemWatcher(snippetDirectoryPath.DirectoryPath, "*.snippet") {
                    NotifyFilter = NotifyFilters.FileName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };
                
                snippetFileWatcher.Deleted += new FileSystemEventHandler(onSnippetFileDeleted);
            });

            _ = Task.Run(() => { _ = StartAutoSyncAsync(); });
        }

        private static async Task onSnippetFileDeletedAsync(object sender, FileSystemEventArgs e) {
            LogInfomation("同步開始");
            //將遠端內容改為"[已刪除]"字樣
            var snippetGist = await GetSnippetGistAsync();
            var updateSnippetGist = new GistUpdate();
            
            var codeLanguage = SnippetGuids.Where(s => e.FullPath.Contains(s.DirectoryName)).First().CodeLanguage;
            var fileName = Path.GetFileName(e.FullPath);
            //e.FullPath.CON
            Console.WriteLine("檔案變更: " + e.FullPath + " " + e.ChangeType);
            //if (![已刪除])
            //updateSnippetGist.Files.Add(gistFile.Key, new GistFileUpdate() { NewFileName = gistFile.Key.Split('|')[0] + "|" + gistFile.Key.Split('|')[1] + "|" + DateTime.MinValue.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"), Content = deletedContextText });
            //if (updateSnippetGist.Files.Count > 0) {
            //    await GitHub.Gist.Edit(snippetGist.Id, updateSnippetGist);
            //}

            if (updateSnippetGist.Files.Count > 0) {
                await GitHub.Gist.Edit(snippetGist.Id, updateSnippetGist);
            }

            LogInfomation("同步完成");
        }

        private static List<SnippetDirectoryPath> getSnippetDirectoryPaths() {
            SnippetDirectoryPathJsonStr = string.IsNullOrWhiteSpace(SnippetDirectoryPathJsonStr) ? JsonConvert.SerializeObject(getSnippetDirectoryPathsFromVS()) : SnippetDirectoryPathJsonStr;

            return JsonConvert.DeserializeObject<List<SnippetDirectoryPath>>(SnippetDirectoryPathJsonStr);
        }

        private static List<SnippetDirectoryPath> getSnippetDirectoryPathsFromVS() {
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

                            //只獲取使用者自訂程式碼片段(排除VS自帶片段)
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

        private static string getGistCodeLanguage(KeyValuePair<string ,GistFile> gistFile) {
            return gistFile.Key.Split('|')[0];
        }
        private static string getGistFileName(KeyValuePair<string, GistFile> gistFile) {
            return gistFile.Key.Split('|')[1];
        }

        private static DateTime getGistFileLastUploadTimeUtc(KeyValuePair<string, GistFile> gistFile) {
            return DateTime.Parse(gistFile.Key.Split('|')[2]).ToUniversalTime();
        }

        public static string SnippetDirectoryPathJsonStr {
            get {
                return UserSettingsStore.GetString(ThisExtensionName, "SnippetDirectoryPathJsonStr", "");
            }
            set {
                UserSettingsStore.SetString(ThisExtensionName, "SnippetDirectoryPathJsonStr", value);
            }
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
                            LogInfomation($"新增檔案[{localFile.FilePath}]");

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
                            var matchedGistFile = snippetGist.Files.ToList().Where(gistFile => getGistCodeLanguage(gistFile) == localFile.CodeLanguage && getGistFileName(gistFile) == localFile.FileName).FirstOrDefault();

                            //本機端存在，但遠端不存在，則上傳
                            if (matchedGistFile.Key == null) {
                                updateSnippetGist.Files.Add(gistNameByLocal, new GistFileUpdate() { NewFileName = gistNameByLocal, Content = localFile.FileCotent });
                            }
                            //本機端存在，且遠端亦存在
                            else {
                                var gistFileLastUploadTimeUtc = getGistFileLastUploadTimeUtc(matchedGistFile);

                                //若本機端較新，則更新遠端
                                if (localFile.LastWriteTimeUtc > gistFileLastUploadTimeUtc) {
                                    updateSnippetGist.Files.Add(matchedGistFile.Key, new GistFileUpdate() { NewFileName = gistNameByLocal, Content = localFile.FileCotent });
                                }
                                //若遠端較新，則更新本機端                                
                                else if (localFile.LastWriteTimeUtc < gistFileLastUploadTimeUtc) {
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

                            var matchedLocalFile = snippetFiles.Where(localFile => localFile.CodeLanguage == getGistCodeLanguage(gistFile) && localFile.FileName == getGistFileName(gistFile)).FirstOrDefault();

                            //本機端不存在，但遠端存在，則下載
                            if (matchedLocalFile == null) {
                                if (gistFile.Value.Content == deletedContextText) {
                                    continue;
                                }

                                var localDirectoryPath = snippetDirectoryPaths.Where(snippetDirectoryPath => snippetDirectoryPath.CodeLanguage == getGistCodeLanguage(gistFile)).First().DirectoryPath;
                                var localFilePath = localDirectoryPath + "\\" + getGistFileName(gistFile);
                                var localFileLastWriteTimeUtc = getGistFileLastUploadTimeUtc(gistFile);

                                File.WriteAllText(localFilePath, gistFile.Value.Content);
                                File.SetLastWriteTimeUtc(localFilePath, localFileLastWriteTimeUtc);
                            }
                            //遠端內容為"[已刪除]"字樣，則刪除本機端
                            else {
                                if (gistFile.Value.Content != deletedContextText) {
                                    continue;
                                }

                                if (matchedLocalFile.LastWriteTimeUtc > getGistFileLastUploadTimeUtc(gistFile)) {
                                    continue;
                                }

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
            var snippetGist = (await GitHub.Gist.GetAllForUser(UserName)).Where(gist => gist.Public == false && gist.Files.TryGetValue(ThisExtensionName, out _)).FirstOrDefault();

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
            snippetDirectoryPaths = getSnippetDirectoryPaths();
        }
    }

    class SnippetFile {
        public string CodeLanguage { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public string FileCotent { get; set; }

    }

    class SnippetDirectoryPath {
        public string CodeLanguage { get; set; }
        public string DirectoryPath { get; set; }
    }

    public class SnippetGuid {
        public string CodeLanguage { get; set; }
        public string DirectoryName { get; set; }
        public Guid Guid { get; set; }
    }
}
