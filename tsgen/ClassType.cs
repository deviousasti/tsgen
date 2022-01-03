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
        Constant,
        Inline,
        Assignment
    }
}
