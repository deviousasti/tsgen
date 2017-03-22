using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Web.Services;
using System.ComponentModel;
using System.Web.Script.Services;
using System.Xml.Serialization;
using JSHints;

namespace tsgen
{
    public enum ClassType
    {
        Default,
        OLN,
        Enum,
        Class,        
        Interface,
        ViewModel,
        WebService,
        SocketService
    }

    public enum JsPropertyType
    {
        Simple,
        Static,
        ReadOnly,
        WriteOnly,
        Attached,
        Observable,
        Dependant,
        ObservableCollection,
        Constant,
        Inline
    }
}
