﻿using System.Xml.Linq;
using static Microsoft.VisualStudio.ProjectSystem.FileSystemMirroring.MsBuild.XProjHelpers;

namespace Microsoft.VisualStudio.ProjectSystem.FileSystemMirroring.MsBuild {
    public class XProject : XElement {
        public XProject() : base(MsBuildNamespace + "Project") { }

        public XProject(string toolsVersion = null, string defaultTargets = null, params object[] elements)
            : base(MsBuildNamespace + "Project", Content(elements, Attr("ToolsVersion", toolsVersion), Attr("DefaultTargets", defaultTargets))) { }
    }
}