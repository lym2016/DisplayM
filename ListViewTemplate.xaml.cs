using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Visualizer.Core.Models;

namespace DisplayM
{
    /// <summary>
    /// ListViewTemplate.xaml 的交互逻辑
    /// </summary>
    public partial class ListViewTemplate : UserControl
    {
        public ListViewTemplate()
        {
            InitializeComponent();
        }

        public void ConfigureColumns(VisualConfig config)
        {
            MainGridView.Columns.Clear();

            foreach (var prop in config.Properties
                .Where(p => p.Value.ModeSettings[DisplayMode.List].Visible)
                .OrderBy(p => p.Value.ModeSettings[DisplayMode.List].Order))
            {
                var column = new GridViewColumn
                {
                    Header = prop.Value.DisplayName,
                    Width = ConvertToDouble(prop.Value.ModeSettings[DisplayMode.List].Width)
                };

                column.CellTemplate = CreateCellTemplate(prop.Value);
                MainGridView.Columns.Add(column);
            }
        }

        private DataTemplate CreateCellTemplate(PropertyVisualConfig propConfig)
        {
            // 根据属性配置创建单元格模板
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(ContentPresenter));

            factory.SetValue(ContentPresenter.ContentTemplateProperty,
                CreateValueTemplate(propConfig));

            template.VisualTree = factory;
            return template;
        }

        private DataTemplate CreateValueTemplate(PropertyVisualConfig propConfig)
        {
            // 根据属性类型和配置创建值显示模板
            // 这里需要根据TypeConfig来决定使用什么控件
            // ...
        }
    }
}
