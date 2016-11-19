using System.ComponentModel.DataAnnotations;

namespace SocialLife.Models
{
    public class GeoCodeLocationViewModel
    {
        [Display(Name = "Center Search on Location")]
        public string Name { get; set; }

        [Display(Name = "Coordinates")]
        public string Coordinates { get; set; }

    }
}