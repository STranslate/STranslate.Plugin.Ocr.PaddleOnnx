using CommunityToolkit.Mvvm.ComponentModel;

namespace STranslate.Plugin.Ocr.PaddleOnnx.ViewModel;

/// <summary>
/// 设置视图模型
/// 当前版本无需配置
/// </summary>
public partial class SettingsViewModel(IPluginContext context, Settings settings) : ObservableObject
{
    // 当前版本不需要配置项
    // 如需添加配置，使用 [ObservableProperty] 定义属性
}
