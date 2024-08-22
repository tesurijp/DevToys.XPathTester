using DevToys.Api;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using static DevToys.Api.GUI;

namespace tesuri.DevToys.XPathTester;

[Export(typeof(IGuiTool))]
[Name("XPathTester")]
[ToolDisplayInformation(
    IconFontName = "FluentSystemIcons",
    IconGlyph = '\uE4F4',
    GroupName = PredefinedCommonToolGroupNames.Testers,
    ResourceManagerAssemblyIdentifier = nameof(TesuriDevToysXPathTesterAssemblyIdentifier),
    ResourceManagerBaseName = "tesuri.DevToys.XPathTester.XPathTester",
    ShortDisplayTitleResourceName = nameof(XPathTester.ShortDisplayTitle),
    LongDisplayTitleResourceName = nameof(XPathTester.LongDisplayTitle),
    DescriptionResourceName = nameof(XPathTester.Description),
    AccessibleNameResourceName = nameof(XPathTester.AccessibleName),
    SearchKeywordsResourceName = nameof(XPathTester.SearchKeywords)
    )]
internal class XPathTesterGui : IGuiTool, IDisposable
{
    private readonly DisposableSemaphore semaphore = new();
    private CancellationTokenSource? cancelToken = null;

    private static readonly SettingDefinition<bool> TrimStyle = new(
            name: $"{nameof(XPathTesterGui)}.{nameof(TrimStyle)}",
            defaultValue: false);

    [Import]
    private ISettingsProvider settingsProvider = null!;

    [Import]
    private IClipboard clipboard = null!;

    private readonly IUIDataGrid resultGrid = DataGrid().WithColumns(XPathTester.OutputResult).Extendable();
    private readonly IUIMultiLineTextInput resultError = MultiLineTextInput().Title(XPathTester.OutputError).ReadOnly().Hide();
    private readonly IUIMultiLineTextInput inputXml = MultiLineTextInput().Title(XPathTester.InputTitle);
    private readonly IUISingleLineTextInput inputXPath = SingleLineTextInput().Title(XPathTester.InputXPathTitle);
    private readonly IUISettingGroup resultStyle = SettingGroup();

    public UIToolView View => new(isScrollable: false,
        SplitGrid().Vertical()
        .LeftPaneLength(new UIGridLength(1, UIGridUnitType.Fraction))
        .WithLeftPaneChild(
            inputXml.OnTextChanged(XmlChanged)
            )

        .RightPaneLength(new UIGridLength(1, UIGridUnitType.Fraction))
        .WithRightPaneChild(
                Stack().Vertical().WithChildren(
                    inputXPath.OnTextChanged(XPathChanged),
                        Setting()
                            .Title(XPathTester.StyleSettingsTrimTitle)
                            .Handle(
                                settingsProvider,
                                TrimStyle,
                                TrimStyleToggled),
                    resultGrid,
                    resultError
                    )
                )
        );


    public void OnDataReceived(string dataTypeName, object? parsedData)
    {
        if (dataTypeName == PredefinedCommonDataTypeNames.Xml && parsedData is string xml)
        {
            inputXml.Text(xml);
        }
    }

    private XDocument? doc = null;
    private string? xpath = null;
    private bool trim = false;

    private void TrimStyleToggled(bool newTrimMode)
    {
        trim = newTrimMode;
        StartTest();
    }

    public void XmlChanged(string xml)
    {
        doc = null;
        cancelToken?.Cancel();
        cancelToken?.Dispose();
        cancelToken = new CancellationTokenSource();
        if (!string.IsNullOrWhiteSpace(xml))
        {
            _ = ParseXMLDoc(xml, cancelToken.Token);
        }
    }
    private async Task ParseXMLDoc(string xml, CancellationToken token)
    {
        using (await semaphore.WaitAsync(token))
        {
            await TaskSchedulerAwaiter.SwitchOffMainThreadAsync(token);
            try
            {
                doc = XDocument.Parse(xml);
                resultError.Hide();
                token.ThrowIfCancellationRequested();
                _ = XPathTestAsync(token);
            }
            catch (Exception e)
            {
                resultError.Show().Text(e.Message);
            }
        }
    }

    public void XPathChanged(string _xpath)
    {
        xpath = _xpath;
        StartTest();
    }

    public void StartTest()
    {
        cancelToken?.Cancel();
        cancelToken?.Dispose();
        cancelToken = new CancellationTokenSource();
        _ = XPathTestAsync(cancelToken.Token);
    }

    private async Task XPathTestAsync(CancellationToken token)
    {
        if (doc == null || string.IsNullOrWhiteSpace(xpath))
        {
            return;
        }
        using (await semaphore.WaitAsync(token))
        {
            await TaskSchedulerAwaiter.SwitchOffMainThreadAsync(token);

            try
            {
                var result = doc.XPathEvaluate(xpath);
                resultError.Hide();

                token.ThrowIfCancellationRequested();

                var items = result as IEnumerable<object> ?? [result];

                var list = items.Select(MakeResultRow).OfType<IUIDataGridRow>().ToArray();
                resultGrid.Show().WithRows(list);
            }

            catch (Exception e)
            {
                resultError.Show().Text(e.Message);
            }
        }
    }

    private string? Trim(string? text)
    {
        var trimed = text?.Trim().Trim('\r', '\n');
        if (trimed == text || !trim)
        {
            return text;
        }
        return Trim(trimed);
    }
    private IUIDataGridRow? MakeResultRow(object obj)
    {
        var txt = obj.ToString();
        var msg = trim ? txt?.Trim() : txt;
        if (msg != null)
        {
            var button = Button().Text(XPathTester.ClipboardCopyButtonTitle).Icon("FluentSystemIcons", '\uF32B')
                .AlignHorizontally(UIHorizontalAlignment.Right)
                .OnClick(() => clipboard.SetClipboardTextAsync(msg));
            var lbl = Label().Text(msg);
            var stack = Stack().Horizontal().WithChildren(button, lbl);
            return Row(msg, Cell(stack));
        }
        return null;
    }

    public void Dispose()
    {
        cancelToken?.Cancel();
        cancelToken?.Dispose();
        semaphore.Dispose();
    }
}
