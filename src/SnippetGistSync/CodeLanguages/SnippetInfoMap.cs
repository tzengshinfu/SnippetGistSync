using System;
using System.Collections.Generic;

namespace SnippetGistSync {
    public static class SnippetInfoMap {
        public static List<SnippetInfo> List  =  new List<SnippetInfo>() {
            new SnippetInfo() { Name = "csharp", Guid = new Guid("694DD9B6-B865-4C5B-AD85-86356E9C88DC") },
            new SnippetInfo() { Name = "vb", Guid = new Guid("3A12D0B8-C26C-11D0-B442-00A0244A1DD2") },
            new SnippetInfo() { Name = "fsharp", Guid = new Guid("bc6dd5a5-d4d6-4dab-a00d-a51242dbaf1b") },
            new SnippetInfo() { Name = "cpp", Guid = new Guid("B2F072B0-ABC1-11D0-9D62-00C04FD9DFD9") },
            new SnippetInfo() { Name = "xaml", Guid = new Guid("CD53C9A1-6BC2-412B-BE36-CC715ED8DD41") },
            new SnippetInfo() { Name = "xml", Guid = new Guid("F6819A78-A205-47B5-BE1C-675B3C7F0B8E") },
            new SnippetInfo() { Name = "typescript", Guid = new Guid("4a0dddb5-7a95-4fbf-97cc-616d07737a77") },
            new SnippetInfo() { Name = "python", Guid = new Guid("bf96a6ce-574f-3259-98be-503a3ad636dd") },
            new SnippetInfo() { Name = "sql", Guid = new Guid("ed1a9c1c-d95c-4dc1-8db8-e5a28707a864") },
            new SnippetInfo() { Name = "html", Guid = new Guid("58E975A0-F8FE-11D2-A6AE-00104BCC7269") },
            new SnippetInfo() { Name = "css", Guid = new Guid("A764E898-518D-11d2-9A89-00C04F79EFC3") },
            new SnippetInfo() { Name = "javascript", Guid = new Guid("71d61d27-9011-4b17-9469-d20f798fb5c0") },
        };
    }

    public class SnippetInfo {
        public string Name;
        public Guid Guid;
    }
}
