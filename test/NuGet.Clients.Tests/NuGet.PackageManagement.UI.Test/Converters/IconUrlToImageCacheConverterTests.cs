using System;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class IconUrlToImageCacheConverterTests
    {
        private static readonly ImageSource DefaultPackageIcon;

        static IconUrlToImageCacheConverterTests()
        {
            DefaultPackageIcon = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr24, null, new byte[3] { 0, 0, 0 }, 3);
            DefaultPackageIcon.Freeze();
        }

        [Fact]
        public void Convert_WithMalformedUrlScheme_ReturnsDefault()
        {
            var iconUrl = new Uri("ttp://fake.com/image.png");

            var converter = new IconUrlToImageCacheConverter();

            var image = converter.Convert(
                iconUrl,
                typeof(ImageSource),
                DefaultPackageIcon,
                Thread.CurrentThread.CurrentCulture);

            Assert.Same(DefaultPackageIcon, image);
        }

        [Fact]
        public void Convert_WhenFileNotFound_ReturnsDefault()
        {
            var iconUrl = new Uri(@"C:\path\to\image.png");

            var converter = new IconUrlToImageCacheConverter();

            var image = converter.Convert(
                iconUrl,
                typeof(ImageSource),
                DefaultPackageIcon,
                Thread.CurrentThread.CurrentCulture);

            Assert.Same(DefaultPackageIcon, image);
        }

        [Fact]
        public void Convert_WithLocalPath_LoadsImage()
        {
            var iconUrl = new Uri(@"resources/packageicon.png", UriKind.Relative);

            var converter = new IconUrlToImageCacheConverter();

            var image = converter.Convert(
                iconUrl,
                typeof(ImageSource),
                DefaultPackageIcon,
                Thread.CurrentThread.CurrentCulture) as BitmapImage;

            Assert.NotNull(image);
            Assert.NotSame(DefaultPackageIcon, image);
            Assert.Equal(iconUrl, image.UriSource);
        }

        [Fact(Skip="Fails on CI. Tracking issue: https://github.com/NuGet/Home/issues/2474")]
        public void Convert_WithValidImageUrl_DownloadsImage()
        {
            var iconUrl = new Uri("http://fake.com/image.png");

            var converter = new IconUrlToImageCacheConverter();

            var image = converter.Convert(
                iconUrl,
                typeof(ImageSource),
                DefaultPackageIcon,
                Thread.CurrentThread.CurrentCulture) as BitmapImage;

            Assert.NotNull(image);
            Assert.NotSame(DefaultPackageIcon, image);
            Assert.Equal(iconUrl, image.UriSource);
        }
    }
}
