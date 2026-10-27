namespace UniversalPOS.Application.Employees;

/// <summary>Job titles offered in the employee form. The key is what is stored; display text depends on the UI language.</summary>
public static class JobTitles
{
    public static IReadOnlyList<(string Key, string Arabic, string English)> All { get; } = new (string, string, string)[]
    {
        ("Pharmacist", "صيدلي", "Pharmacist"),
        ("PharmacyAssistant", "مساعد صيدلي", "Pharmacy Assistant"),
        ("Cashier", "كاشير", "Cashier"),
        ("Accountant", "محاسب", "Accountant"),
        ("StoreKeeper", "أمين مخزن", "Store Keeper"),
        ("BranchManager", "مدير فرع", "Branch Manager"),
        ("Worker", "عامل", "Worker")
    };

    public static bool IsValid(string? key) => All.Any(j => j.Key == key);

    public static string Display(string key, bool arabic)
    {
        var job = All.FirstOrDefault(j => j.Key == key);
        return job.Key is null ? key : arabic ? job.Arabic : job.English;
    }
}
