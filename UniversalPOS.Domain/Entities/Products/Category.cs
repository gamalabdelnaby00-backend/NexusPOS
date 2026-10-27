using System;
using System.Collections.Generic;
using System.Text;

namespace UniversalPOS.Domain.Entities
{
    public class Category
    {
        public int Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }
        /// <summary>Optional business code shown to users; unique when present.</summary>
        public string? Code { get; private set; }
        public Category(string name, string? code = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Category name cannot be empty.");
            Name = name;
            Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
            IsActive = true;
        }
        public void Deactivate()
        {
            IsActive = false;
        }

        public void Activate()
        {
            IsActive = true;
        }
    }
}
