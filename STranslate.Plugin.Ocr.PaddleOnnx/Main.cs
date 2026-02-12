using RapidOcrNet;
using SkiaSharp;
using STranslate.Plugin.Ocr.PaddleOnnx.View;
using STranslate.Plugin.Ocr.PaddleOnnx.ViewModel;
using System.IO;
using Control = System.Windows.Controls.Control;

namespace STranslate.Plugin.Ocr.PaddleOnnx;

/// <summary>
/// PaddleOCR Onnx 插件主类
/// 基于 RapidOcrNet 实现
/// </summary>
public class Main : IOcrPlugin
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;

    /// <summary>
    /// 模型文件所在目录
    /// </summary>
    private string _modelsDirectory = string.Empty;

    /// <summary>
    /// 支持的语言列表
    /// </summary>
    public IEnumerable<LangEnum> SupportedLanguages =>
    [
        LangEnum.Auto,
        LangEnum.ChineseSimplified,
        LangEnum.ChineseTraditional,
        LangEnum.English,
        LangEnum.Korean,
        LangEnum.Japanese,
    ];

    /// <summary>
    /// 获取设置界面
    /// </summary>
    public Control GetSettingUI()
    {
        _viewModel ??= new SettingsViewModel(Context, Settings);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    /// <summary>
    /// 初始化插件
    /// </summary>
    public void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();

        // 设置模型目录为插件目录下的 models 文件夹
        var pluginDirectory = Path.GetDirectoryName(context.MetaData.PluginDirectory)
            ?? AppContext.BaseDirectory;
        _modelsDirectory = Path.Combine(pluginDirectory, context.MetaData.AssemblyName, "models", "v5");
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        // TODO: 释放 OCR 引擎资源
    }

    /// <summary>
    /// 异步识别图片中的文本
    /// </summary>
    public async Task<OcrResult> RecognizeAsync(OcrRequest request, CancellationToken cancellationToken)
    {
        var result = new OcrResult();

        try
        {
            // 创建超时取消令牌（默认30秒超时）
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // 在线程池中异步执行同步的OCR操作
            var ocrResult = await Task.Run(() =>
            {
                combinedCts.Token.ThrowIfCancellationRequested();

                // 初始化 RapidOcr 引擎并加载模型（使用插件目录下的模型）
                using var ocrEngine = new RapidOcr();
                var detPath = Path.Combine(_modelsDirectory, "ch_PP-OCRv5_mobile_det.onnx");
                var clsPath = Path.Combine(_modelsDirectory, "ch_ppocr_mobile_v2.0_cls_infer.onnx");
                var recPath = Path.Combine(_modelsDirectory, "latin_PP-OCRv5_rec_mobile_infer.onnx");
                var keysPath = Path.Combine(_modelsDirectory, "ppocrv5_latin_dict.txt");
                ocrEngine.InitModels(detPath, clsPath, recPath, keysPath, 0);

                combinedCts.Token.ThrowIfCancellationRequested();

                // 将 byte[] 解码为 SKBitmap
                using var bitmap = SKBitmap.Decode(request.ImageData)
                    ?? throw new InvalidOperationException("无法解码图片数据");

                combinedCts.Token.ThrowIfCancellationRequested();

                // 执行 OCR 识别
                var detectResult = ocrEngine.Detect(bitmap, RapidOcrOptions.Default);

                // 转换结果为 OcrResult
                return ConvertToOcrResult(detectResult);
            }, combinedCts.Token);

            return ocrResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return result.Fail("操作已被取消");
        }
        catch (OperationCanceledException)
        {
            return result.Fail("识别操作超时（30秒）");
        }
        catch (Exception ex)
        {
            return result.Fail($"识别过程中发生错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 将 RapidOcrNet 的识别结果转换为 STranslate 的 OcrResult
    /// </summary>
    private static OcrResult ConvertToOcrResult(RapidOcrNet.OcrResult detectResult)
    {
        var result = new OcrResult();

        if (detectResult?.TextBlocks is not { Length: > 0 })
        {
            return result.Fail("识别结果为空");
        }

        foreach (var block in detectResult.TextBlocks)
        {
            var ocrContent = new OcrContent
            {
                Text = block.GetText()
            };

            // 添加边界框坐标点
            if (block.BoxPoints != null && block.BoxPoints.Length >= 4)
            {
                foreach (var point in block.BoxPoints)
                {
                    ocrContent.BoxPoints.Add(new BoxPoint(point.X, point.Y));
                }
            }

            result.OcrContents.Add(ocrContent);
        }

        return result;
    }
}
