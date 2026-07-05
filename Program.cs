using NetTopologySuite.Features;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using static System.Net.Mime.MediaTypeNames;

namespace Earthquake_ASPNET_App {
    public partial class Program {

        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapRazorPages()
               .WithStaticAssets();

            app.Run();

            await setImageAsync();
        }


        /// <summary>
        /// 住所等から市区町村名だけを取り出す関数
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static string ConvertLocationName(string name)
        {
            int index = -1;
            if (name.IndexOf('市') > 0) index = name.IndexOf('市');
            else if (name.IndexOf('村') > 0) index = name.IndexOf('村');
            else if (name.IndexOf('町') > 0) index = name.IndexOf('町');
            else if (name.IndexOf('区') > 0) index = name.IndexOf('区');

            if (index == -1)
            {
                return null;
            }
            else
            {
                return name.AsSpan(0, index + 1).ToString();
            }
        }

        /// <summary>
        /// IFeatureからシェイプファイルの属性（regionname）に実際にあるかを返す関数
        /// </summary>
        /// <param name="feature"></param>
        /// <returns>実際にある場合は実際のキーを返し、無い場合はnullを返す。</returns>
        public static string FindKey(string fieldName, IFeature feature)
        {
            var key = feature.Attributes
                            .GetNames().FirstOrDefault(a =>
                            a.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

            if (key == null) return null;
            if (!feature.Attributes.Exists(key)) return null;

            var name = feature.Attributes[key]?.ToString();
            if (string.IsNullOrEmpty(name)) return null;

            return feature.Attributes[key]?.ToString();
        }

        public static async Task setImageAsync()
        {
            MapRender backDrawMap = new("./Resource/市町村等.shp", 1000, 1000);
            MapRender forwardDrawMap = new("./Resource/府県予報区等.shp", 1000, 1000);
            DateTime beforeTime = DateTime.Now;
            await Task.Run(() => {
                var earthquakeData = EarthquakeData.GetInstance()[0];
                var earthquake = earthquakeData.earthquake;
                var hypocenter = earthquake.hypocenter;

                // 各震度地点ごとに色設定
                foreach (var point in earthquakeData.points)
                {
                    Func<IFeature, bool> filter = feature => {
                        string key = FindKey("regionname", feature);
                        if (key == null) return false;
                        return key.Contains($"{point.pref}{ConvertLocationName(point.addr)}");
                    };
                    backDrawMap.SetFill(EarthquakeData.ConvertScaleColor(point.scale), filter);  // 地域ごとに個別色設定
                }

                backDrawMap.AddLine(Color.Black, 1, f => true); // 境界線は黒色
                forwardDrawMap.AddLine(Color.Black, 3, f => true);

                // 描画範囲
                Func<IFeature, bool> backDrawFilter = feature => {
                    return earthquakeData.points.Any(p => {
                        string key = FindKey("regionname", feature);
                        if (key == null) return false;
                        return key.Contains($"{p.pref}");
                    });
                };

                Func<IFeature, bool> forwardDrawFilter = feature => {
                    return earthquakeData.points.Any(p => {
                        string key = FindKey("name", feature);
                        if (key == null) return false;
                        return key.Contains($"{p.pref}");
                    });
                };



                backDrawMap.SetDrawArea(backDrawFilter);   // 描画範囲を設定する
                forwardDrawMap.SetDrawArea(forwardDrawFilter);
                forwardDrawMap.AddImage("./Resource/point.png", hypocenter.longitude, hypocenter.latitude);



                var backDrawMapBuild = backDrawMap.Build();
                var forwardDrawMapBuild = forwardDrawMap.Build();
                //forwardDrawMapBuild.Save("debug2.png");



                using Bitmap image = new(1000, 1000);
                using (Graphics graphics = Graphics.FromImage(image))
                {
                    graphics.DrawImage(backDrawMapBuild, 0, 0);
                    graphics.DrawImage(forwardDrawMapBuild, 0, 0);
                }

                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "debug.png");
                image.Save(path, ImageFormat.Png);

                DateTime afterTime = DateTime.Now;
                TimeSpan subtractTime = beforeTime.Subtract(afterTime);
                Debug.Write("描画に成功しました：" + subtractTime.ToString("hh\\:mm\\:ss"));
            });
        }

    }
}