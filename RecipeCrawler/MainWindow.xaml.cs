using System.Collections.Generic;
using System.Windows;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Linq;
using System;
using System.Net;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows.Navigation;

namespace RecipeCrawler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        struct Response
        {
            public string Title { get; set; }
            public string Verson { get; set; }
            public string Href { get; set; }
            public List<Recipe> Results { get; set; }
        }

        struct Recipe
        {
            public string Title { get; set; }
            public string Href { get; set; }
            public string Ingredients { get; set; }
            public string Thumbnail { get; set; }

            [JsonIgnore]
            public string UniqueIngredients { get; set; }


            public Recipe(Recipe r)
            {
                this.Title = WebUtility.HtmlDecode(r.Title.Trim().Replace("\n", "").Replace("\r", ""));
                this.Href = r.Href.Trim();
                this.Ingredients = r.Ingredients.Trim();
                this.Thumbnail = r.Thumbnail.Trim();

                this.UniqueIngredients = "";
            }

            public void SetHighlighted(string text)
            {
                UniqueIngredients = text;
            }
        }

        public List<string> ingredients;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            StatusLabel.Content = "Searching...";
            StatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160));
            RecipeItemControl.ItemsSource = null;
            UpdateLayout();

            ingredients = Regex
                    .Matches(SearchBox.Text, "[a-zA-Z-]+")
                    .Cast<Match>()
                    .Select(v => v.Value)
                    .ToList();

            Task.Run(action: DoSearchQuery);
        }

        private void DoSearchQuery()
        {
            List<Recipe> recipes = new List<Recipe>();
            try
            {
                int lastCount = 0;
                using (var client = new WebClient())
                {
                    var query = String.Join(",", ingredients);
                    int page = 1;
                    while (true)
                    {
                        var data = client.DownloadString($"http://www.recipepuppy.com/api/?i={query}&p={page}");
                        recipes.AddRange(JsonConvert.DeserializeObject<Response>(data).Results);

                        if (recipes.Count == lastCount)
                            break;

                        if (recipes.Any(r => r.Ingredients.Count(c => c == ',') + 1 == ingredients.Count))
                            break;

                        lastCount = recipes.Count;
                        ++page;
                    }
                }
            }
            catch (Exception e)
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    StatusLabel.Content = e.Message;
                    StatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(220, 130, 130));
                }));
                return;
            }

            recipes.Sort(Comparer<Recipe>.Create(
                (a, b) => a.Ingredients.Count(c => c == ',') - b.Ingredients.Count(c => c == ',')
            ));

            recipes = recipes
                .Take(10)
                .Select(r => new Recipe(r)
                {
                    UniqueIngredients = "Additional ingredients: " + (String.Join(", ",
                        Regex
                        .Matches(r.Ingredients, "[a-zA-Z-]+")
                        .Cast<Match>()
                        .Select(v => v.Value)
                        .Where(v => !ingredients.Contains(v))
                        .DefaultIfEmpty("--none--")
                    ))
                })
                .Where(
                    r => r.Ingredients.Count(c => c == ',') == recipes.Min(
                    m => m.Ingredients.Count(c => c == ',')))
                .ToList();

            if (recipes.Count == 0)
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    StatusLabel.Content = "Nothing was found :(";
                    StatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(220, 130, 130));
                }));
                return;
            }

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                StatusLabel.Content = "";
                RecipeItemControl.ItemsSource = recipes;
                RecipeItemControl.DataContext = recipes;
                UpdateLayout();
            }));
        }

        private void LinkOnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }
    }
}
