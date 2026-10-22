using System.Windows.Controls;
using System.Windows.Input;
using FlowChartKit.Rendering;
using FlowChartKit.ViewModels;

namespace FlowChartKit.Controls;

/// <summary>
/// 画布右键菜单构建器：按当前选中状态重建菜单项。
/// 选中管路 → 流向/属性/删除；选中部件 → 属性/删除；
/// 空白处 → 添加部件/管路；始终附加编辑模式/保存布局/清空布局。
/// </summary>
internal static class CanvasContextMenuBuilder
{
    public static void Build(ContextMenu menu, DiagramCanvasControl host)
    {
        var diagram = host.Diagram;
        menu.Items.Clear();

        if (diagram.SelectedConnection is not null)
        {
            menu.Items.Add(NewItem(FlowModeDefaults.DisplayName(FlowMode.Forward), diagram.SetDirectionCommand, FlowMode.Forward));
            menu.Items.Add(NewItem(FlowModeDefaults.DisplayName(FlowMode.Reverse), diagram.SetDirectionCommand, FlowMode.Reverse));
            menu.Items.Add(NewItem(FlowModeDefaults.DisplayName(FlowMode.None), diagram.SetDirectionCommand, FlowMode.None));
            menu.Items.Add(new Separator());
            menu.Items.Add(NewItem("属性", host.ShowPropertiesCommand, null));
            menu.Items.Add(NewItem("删除管路", diagram.DeleteSelectedCommand, null));
        }
        else if (diagram.SelectedTextBlock is not null)
        {
            menu.Items.Add(NewItem("属性", host.ShowPropertiesCommand, null));
            menu.Items.Add(NewItem("删除文本", diagram.DeleteSelectedCommand, null));
        }
        else if (diagram.SelectedNode is not null)
        {
            menu.Items.Add(NewItem("属性", host.ShowPropertiesCommand, null));
            menu.Items.Add(NewItem("删除部件", diagram.DeleteSelectedCommand, null));
        }
        else
        {
            var addNode = new MenuItem { Header = "添加部件" };
            // 两级分组：内置定义（注册表内置层）与扩展定义（宿主经覆盖层注册，含对内置类型的替换）
            var builtInGroup = new MenuItem { Header = "内置部件" };
            var customGroup = new MenuItem { Header = "扩展部件" };
            foreach (var def in NodeDefinitionRegistry.All)
                (NodeDefinitionRegistry.IsBuiltIn(def.Kind) ? builtInGroup : customGroup)
                    .Items.Add(NewItem(def.DisplayName, host.AddNodeCommand, def.Kind));
            addNode.Items.Add(builtInGroup);
            if (customGroup.Items.Count > 0)
                addNode.Items.Add(customGroup);
            menu.Items.Add(addNode);
            menu.Items.Add(NewItem("添加文本模块", host.AddTextItemCommand, null));
            menu.Items.Add(NewAddConnectionMenu(host));
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(NewEditableItem(host));
        menu.Items.Add(NewSnapItem(host));
        menu.Items.Add(NewItem("保存布局", host.SaveLayoutCommand, null));
        menu.Items.Add(NewItem("清空布局", host.ClearLayoutCommand, null));
    }

    private static MenuItem NewItem(string header, ICommand command, object? parameter)
        => new() { Header = header, Command = command, CommandParameter = parameter };

    /// <summary>
    /// "添加管路"菜单：首项按 <see cref="DiagramCanvasControl.DefaultPipeKind"/> 直接添加；
    /// 其后按注册表分组列出全部管路类型（内置管路 / 扩展管路），宿主注册的自定义类型供挑选。
    /// </summary>
    private static MenuItem NewAddConnectionMenu(DiagramCanvasControl host)
    {
        var addConnection = new MenuItem { Header = "添加管路" };

        var defaultName = PipeDefinitionRegistry.IsAvailable(host.DefaultPipeKind)
            ? PipeDefinitionRegistry.Resolve(host.DefaultPipeKind).DisplayName
            : PipeDefinitionRegistry.Resolve(PipeKind.Standard).DisplayName;
        addConnection.Items.Add(NewItem($"默认（{defaultName}）", host.AddConnectionCommand, null));

        var builtInGroup = new MenuItem { Header = "内置管路" };
        var customGroup = new MenuItem { Header = "扩展管路" };
        foreach (var def in PipeDefinitionRegistry.All)
            (PipeDefinitionRegistry.IsBuiltIn(def.Kind) ? builtInGroup : customGroup)
                .Items.Add(NewItem(def.DisplayName, host.AddConnectionCommand, def.Kind));

        addConnection.Items.Add(builtInGroup);
        if (customGroup.Items.Count > 0)
            addConnection.Items.Add(customGroup);
        return addConnection;
    }

    private static MenuItem NewEditableItem(DiagramCanvasControl host)
    {
        // 走依赖属性而非直接改 Diagram，保证外部绑定与图模型始终一致
        var item = new MenuItem { Header = "编辑模式", IsCheckable = true, IsChecked = host.IsEditable };
        item.Click += (_, _) => host.IsEditable = item.IsChecked == true;
        return item;
    }

    /// <summary>
    /// "对齐吸附"开关：关闭后拖动部件/管路线段不再自动吸附，便于像素级精细调整；
    /// 临时关闭可按住 Alt 拖动（只作用于当次拖动）。
    /// </summary>
    private static MenuItem NewSnapItem(DiagramCanvasControl host)
    {
        var item = new MenuItem
        {
            Header = "对齐吸附（拖动时按住 Alt 临时关闭）",
            IsCheckable = true,
            IsChecked = host.AlignmentSnapEnabled,
        };
        item.Click += (_, _) => host.AlignmentSnapEnabled = item.IsChecked == true;
        return item;
    }
}
