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
        else if (diagram.SelectedNode is not null)
        {
            menu.Items.Add(NewItem("属性", host.ShowPropertiesCommand, null));
            menu.Items.Add(NewItem("删除部件", diagram.DeleteSelectedCommand, null));
        }
        else
        {
            var addNode = new MenuItem { Header = "添加部件" };
            // 枚举注册表当前生效的定义（内置 + 宿主自定义），而非仅枚举值
            foreach (var def in NodeDefinitionRegistry.All)
                addNode.Items.Add(NewItem(def.DisplayName, host.AddNodeCommand, def.Kind));
            menu.Items.Add(addNode);
            menu.Items.Add(NewItem("添加管路", host.AddConnectionCommand, null));
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(NewEditableItem(host));
        menu.Items.Add(NewItem("保存布局", host.SaveLayoutCommand, null));
        menu.Items.Add(NewItem("清空布局", host.ClearLayoutCommand, null));
    }

    private static MenuItem NewItem(string header, ICommand command, object? parameter)
        => new() { Header = header, Command = command, CommandParameter = parameter };

    private static MenuItem NewEditableItem(DiagramCanvasControl host)
    {
        // 走依赖属性而非直接改 Diagram，保证外部绑定与图模型始终一致
        var item = new MenuItem { Header = "编辑模式", IsCheckable = true, IsChecked = host.IsEditable };
        item.Click += (_, _) => host.IsEditable = item.IsChecked == true;
        return item;
    }
}
