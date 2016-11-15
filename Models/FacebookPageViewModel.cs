using SocialLife.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SocialLife.Models
{
    public class FacebookPageViewModel
    {
        [Required]
        [FacebookMapping("id")]
        public string Id { get; set; }

        [Required]
        [FacebookMapping("name")]
        public string Name { get; set; }

        [FacebookMapping("link")]
        public string PageURL { get; set; }

        [Display(Name = "# Likes")]
        [FacebookMapping("likes")]
        public long Likes { get; set; }

        [Display(Name = "# Talking")]
        [FacebookMapping("talking_about_count")]
        public long TalkingCount { get; set; }

        [Display(Name = "Visible")]
        [FacebookMapping("is_published")]
        public bool Published { get; set; }

    }
}
