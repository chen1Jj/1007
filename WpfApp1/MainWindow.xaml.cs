using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        // 1. 使用 Dictionary 儲存品項與單價（Key: 品名, Value: 單價）
        private readonly Dictionary<string, int> menuDict = new Dictionary<string, int>();

        public MainWindow()
        {
            InitializeComponent();
        }

        // 2. OpenFileDialog 讀取 CSV 與 File 靜態類別操作
        private void btnLoadCsv_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "CSV 檔案 (*.csv)|*.csv|所有檔案 (*.*)|*.*",
                Title = "請選擇飲料菜單 CSV 檔"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                DrinkMenuStackPanel.Children.Clear();
                menuDict.Clear();

                try
                {
                    // 使用 System.IO.File 靜態類別讀取全部行數
                    string[] lines = File.ReadAllLines(openFileDialog.FileName, Encoding.UTF8);
                    int itemCount = 0;

                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        string[] parts = line.Split(',');
                        if (parts.Length >= 2)
                        {
                            string itemName = parts[0].Trim();
                            if (int.TryParse(parts[1].Trim(), out int itemPrice))
                            {
                                // 寫入 Dictionary 快取 (注意 Key 唯一性)
                                if (!menuDict.ContainsKey(itemName))
                                {
                                    menuDict.Add(itemName, itemPrice);
                                }

                                // 3. 動態生成 UI 元件與 Dynamic Data Binding
                                CreateDynamicRowUI(itemName, itemPrice);
                                itemCount++;
                            }
                        }
                    }

                    txtSummary.Text = $"菜單載入成功！共載入 {itemCount} 項飲料品項。";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"讀取 CSV 檔案失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 3. 動態生成 UI 與 Data Binding 設定
        private void CreateDynamicRowUI(string name, int price)
        {
            // 實例化水平 StackPanel 當作一列容器
            StackPanel rowPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 4),
                Tag = name
            };

            // 實例化 CheckBox
            CheckBox chk = new CheckBox
            {
                Content = name,
                Width = 140,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 實例化價格 Label
            Label lb_price = new Label
            {
                Content = $"{price}元",
                Width = 70,
                FontSize = 14,
                Foreground = System.Windows.Media.Brushes.DarkRed,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 實例化 Slider
            Slider sld = new Slider
            {
                Minimum = 0,
                Maximum = 10,
                Width = 180,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 0)
            };

            // 實例化數量 Label (lb_amount)
            Label lb_amount = new Label
            {
                Width = 50,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };

            // 4. 動態 Data Binding: 透過 C# 程式碼設定 lb_amount 與 Slider 的連動
            Binding binding = new Binding
            {
                Source = sld,
                Path = new PropertyPath("Value"),
                StringFormat = "{0:F0}"
            };
            lb_amount.SetBinding(Label.ContentProperty, binding);

            // 加入子控制項至列容器
            rowPanel.Children.Add(chk);
            rowPanel.Children.Add(lb_price);
            rowPanel.Children.Add(sld);
            rowPanel.Children.Add(lb_amount);

            // 加入列容器至畫面的 DrinkMenuStackPanel
            DrinkMenuStackPanel.Children.Add(rowPanel);
        }

        // 5. 視窗樹狀走訪與 SaveFileDialog 匯出存檔
        private void btnSaveOrder_Click(object sender, RoutedEventArgs e)
        {
            if (DrinkMenuStackPanel.Children.Count == 0)
            {
                MessageBox.Show("請先載入 CSV 菜單檔案！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 取得內用 / 外帶設定
            string diningType = rbDineIn.IsChecked == true ? "內用" : "外帶";

            StringBuilder summary = new StringBuilder();
            summary.AppendLine($"【飲料訂購單】 建立時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            summary.AppendLine($"【用餐方式】: {diningType}");
            summary.AppendLine("----------------------------------------");

            int rawTotal = 0;
            int totalItems = 0;

            // 5.1 視窗樹狀走訪：走訪 DrinkMenuStackPanel.Children 中的每個列元件
            foreach (UIElement child in DrinkMenuStackPanel.Children)
            {
                if (child is StackPanel rowPanel)
                {
                    // 從 rowPanel 提取對應的子元件
                    CheckBox? chk = rowPanel.Children[0] as CheckBox;
                    Slider? sld = rowPanel.Children[2] as Slider;

                    if (chk != null && sld != null)
                    {
                        string itemName = chk.Content.ToString() ?? "";
                        int qty = (int)sld.Value;

                        if (chk.IsChecked == true && qty > 0)
                        {
                            // 使用 Dictionary 的 TryGetValue 進行安全讀取
                            if (menuDict.TryGetValue(itemName, out int price))
                            {
                                int itemTotal = price * qty;
                                rawTotal += itemTotal;
                                totalItems += qty;
                                summary.AppendLine($"{itemName}\t x {qty}杯 \t= {itemTotal}元");
                            }
                        }
                    }
                }
            }

            if (totalItems == 0)
            {
                MessageBox.Show("請勾選欲購買的品項並選擇數量！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            summary.AppendLine("----------------------------------------");
            summary.AppendLine($"原始小計：{rawTotal} 元");

            // 折扣演算法 (滿 200 打 9 折)
            double finalTotal = rawTotal;
            if (rawTotal >= 200)
            {
                finalTotal = Math.Round(rawTotal * 0.9);
                summary.AppendLine("優惠折扣：滿 200 元享 9 折優惠！");
            }

            summary.AppendLine($"實付金額：{finalTotal} 元");

            // 更新畫面明細
            txtSummary.Text = summary.ToString();

            // 5.2 SaveFileDialog 與 DefaultExt / OverwritePrompt 設定
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "文字檔案 (*.txt)|*.txt",
                DefaultExt = "txt",
                OverwritePrompt = true,
                FileName = $"訂單明細_{diningType}_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Title = "儲存訂單明細文字檔"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 使用 System.IO.File 靜態類別寫入文字檔
                    File.WriteAllText(saveFileDialog.FileName, summary.ToString(), Encoding.UTF8);
                    MessageBox.Show("訂單明細已成功匯出存檔！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"寫入檔案失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    // 輔助類別 (加上 = null!; 以消除 CS8618 警告)
    public class OrderItemUI
    {
        public CheckBox CheckBox { get; set; } = null!;
        public Slider Slider { get; set; } = null!;
        public string ItemName { get; set; } = null!;
        public int Price { get; set; }
    }
}