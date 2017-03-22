using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public abstract class JSRenderable
{
    public string Name { get; set; }

    public string Description { get; set; }

    public JSRenderable()
    {
        AppendNewLine = true;
    }

    public int NestedLevel { get; set; }

    public virtual IEnumerable<JSRenderable> Children()
    {
        return Enumerable.Empty<JSRenderable>();
    }

    public bool AppendNewLine { get; set; }

    protected abstract void OnRender(StringBuilder buffer);

    public void Render(StringBuilder buffer)
    {
        foreach (var item in Children())
        {
            if (item == null)
                continue;

            item.NestedLevel = NestedLevel + 1;
        }

        OnRender(buffer);
    }

    public void Resolve(StringBuilder buffer)
    {
        OnResolve(buffer);
    }

    protected virtual void OnResolve(StringBuilder buffer)
    {
        
    }

    protected static void BufferList(StringBuilder buffer, IEnumerable<JSRenderable> items, string customFormat = "", string blockTerminator = "")
    {
        var itemsArray = items.Where(o => o != null).ToArray();

        for (int i = 0; i < itemsArray.Length; i++)
        {
            JSRenderable item = itemsArray[i];

            buffer.AppendFormat(customFormat, item.Name, item.NestedLevel, Environment.NewLine);

            item.Render(buffer);

            if (i < itemsArray.Length - 1)
                if (item.AppendNewLine)
                    buffer.AppendLine(blockTerminator + Environment.NewLine);
                else
                    buffer.Append(blockTerminator);
        }
    }

    protected static void BufferListAsOLN(StringBuilder buffer, IEnumerable<JSRenderable> items)
    {
        BufferListAsCSV(buffer, items, "{0} : ");
    }

    protected static void BufferListAsCSV(StringBuilder buffer, IEnumerable<JSRenderable> items, string customFormat)
    {
        BufferList(buffer, items, customFormat, ", ");
    }

    public string ToJS()
    {
        StringBuilder buffer = new StringBuilder();

        Render(buffer);

        return buffer.ToString();
    }

    protected virtual void WriteDescription(StringBuilder buffer)
    {
        if (!string.IsNullOrEmpty(Description))
        {
            foreach (var line in Description.Trim().Split('\n'))
            {
                buffer.AppendFormat(@"* {0}", line);
                buffer.AppendLine();
            }            
        }
    }
}
