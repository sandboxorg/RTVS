﻿using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.R.Package.DataInspect {
    [Guid("3F6855E6-E2DB-46F2-9820-EDC794FE8AFE")]
    public class VariableGridWindowPane : ToolWindowPane {
        public VariableGridWindowPane() {
            Caption = "Variable Grid";  // TODO: temporary value
            Content = new VariableGridHost();

            BitmapImageMoniker = KnownMonikers.VariableProperty;    // TODO: same icon as Variable Explorer. Is it O.K.? This appears on the tab
        }
    }
}