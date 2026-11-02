using System;

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
            UpdateDetails(name, code);
            IsActive = true;
        }

        public void UpdateDetails(string name, string? code = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Category name cannot be empty.", nameof(name));
            if (name.Trim().Length > 200)
                throw new ArgumentException("Category name cannot exceed 200 characters.", nameof(name));
            if (!string.IsNullOrWhiteSpace(code) && code.Trim().Length > 50)
                throw new ArgumentException("Category code cannot exceed 50 characters.", nameof(code));

            Name = name.Trim();
            Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        }

        public void Deactivate() => IsActive = false;
        public void Activate() => IsActive = true;
    }
}
