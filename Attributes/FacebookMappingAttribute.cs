using System;

namespace SocialLife.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple=true)]
    public class FacebookMapping : Attribute
    {
        private string name;

        public string parent;

        public FacebookMapping(string name)
        {
            this.name = name;

            //default value
            this.parent = string.Empty;
        }

        public string GetName()
        {
           return this.name;
        }
    }
}