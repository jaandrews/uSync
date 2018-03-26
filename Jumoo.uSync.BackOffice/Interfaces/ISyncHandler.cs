﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Jumoo.uSync.Core;

namespace Jumoo.uSync.BackOffice
{
    public interface ISyncHandler: ISyncHandlerBase
    {
        void RegisterEvents();

        IEnumerable<uSyncAction> ImportAll(string folder, bool force, bool includeChildren = true);
        IEnumerable<uSyncAction> ExportAll(string folder);

        IEnumerable<uSyncAction> Report(string folder);
    }
}
