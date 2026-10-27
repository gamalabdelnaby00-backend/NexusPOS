
namespace UniversalPOS.Application.Security;

public static class PermissionCodes
{
    public static class Users
    {
        public const string View = "Users.View";
        public const string Create = "Users.Create";
        public const string Edit = "Users.Edit";
        public const string Deactivate = "Users.Deactivate";
        public const string ResetPassword = "Users.ResetPassword";
        public const string AssignRoles = "Users.AssignRoles";
        public const string ViewPermissions = "Users.ViewPermissions";
        public const string ManageOverrides = "Users.ManageOverrides";
    }

    public static class Roles
    {
        public const string View = "Roles.View";
        public const string Create = "Roles.Create";
        public const string Edit = "Roles.Edit";
        public const string Deactivate = "Roles.Deactivate";
        public const string ManagePermissions = "Roles.ManagePermissions";
    }

    public static class Employees
    {
        public const string View = "Employees.View";
        public const string Create = "Employees.Create";
        public const string Edit = "Employees.Edit";
        public const string Deactivate = "Employees.Deactivate";
    }

    public static class Permissions
    {
        public const string View = "Permissions.View";
    }

    public static class Sales
    {
        public const string View = "Sales.View";
        public const string Create = "Sales.Create";
        public const string AddItems = "Sales.AddItems";
        public const string ChangeQuantity = "Sales.ChangeQuantity";
        public const string ApplyDiscount = "Sales.ApplyDiscount";
        public const string ApplyHighDiscount = "Sales.ApplyHighDiscount";
        public const string RegisterPayment = "Sales.RegisterPayment";
        public const string RegisterSplitPayment = "Sales.RegisterSplitPayment";
        public const string Complete = "Sales.Complete";
        public const string Cancel = "Sales.Cancel";
        public const string CancelCompleted = "Sales.CancelCompleted";
        public const string ViewDetails = "Sales.ViewDetails";
        public const string ReprintReceipt = "Sales.ReprintReceipt";
        public const string ChangePrice = "Sales.ChangePrice";
        /// <summary>Sell without an open cash shift (no drawer is affected). Admin only by default.</summary>
        public const string SellWithoutShift = "Sales.SellWithoutShift";
    }

    public static class Returns
    {
        public const string View = "Returns.View";
        public const string Create = "Returns.Create";
        public const string RefundCash = "Returns.RefundCash";
        public const string RefundToOriginalMethod = "Returns.RefundToOriginalMethod";
        public const string Approve = "Returns.Approve";
        public const string WithoutReceipt = "Returns.WithoutReceipt";
        public const string ViewHistory = "Returns.ViewHistory";
    }

    public static class Shifts
    {
        public const string ViewOwn = "Shifts.ViewOwn";
        public const string ViewAll = "Shifts.ViewAll";
        public const string Open = "Shifts.Open";
        public const string Close = "Shifts.Close";
        public const string CountCash = "Shifts.CountCash";
        public const string ViewExpectedCash = "Shifts.ViewExpectedCash";
        public const string Reconcile = "Shifts.Reconcile";
        public const string ApproveDifference = "Shifts.ApproveDifference";
        public const string Reopen = "Shifts.Reopen";
    }

    public static class CashMovements
    {
        public const string ViewOwn = "CashMovements.ViewOwn";
        public const string ViewAll = "CashMovements.ViewAll";
        public const string AddIn = "CashMovements.AddIn";
        public const string AddOut = "CashMovements.AddOut";
        public const string Reverse = "CashMovements.Reverse";
    }

    public static class Products
    {
        public const string View = "Products.View";
        public const string Create = "Products.Create";
        public const string Edit = "Products.Edit";
        public const string Deactivate = "Products.Deactivate";
        public const string ChangePrice = "Products.ChangePrice";
        public const string ChangeCost = "Products.ChangeCost";
        public const string ManageBarcode = "Products.ManageBarcode";
    }

    public static class Categories
    {
        public const string View = "Categories.View";
        public const string Create = "Categories.Create";
        public const string Edit = "Categories.Edit";
        public const string Deactivate = "Categories.Deactivate";
    }

    public static class Units
    {
        public const string View = "Units.View";
        public const string Manage = "Units.Manage";
    }

    public static class Inventory
    {
        public const string ViewBalances = "Inventory.ViewBalances";
        public const string ViewMovements = "Inventory.ViewMovements";
        public const string ReceiveStock = "Inventory.ReceiveStock";
        public const string AdjustStock = "Inventory.AdjustStock";
        public const string ApproveAdjustment = "Inventory.ApproveAdjustment";
        public const string CountStock = "Inventory.CountStock";
        public const string ApproveCount = "Inventory.ApproveCount";
        public const string TransferStock = "Inventory.TransferStock";
        public const string ViewCost = "Inventory.ViewCost";
        public const string ViewValuation = "Inventory.ViewValuation";
        public const string Export = "Inventory.Export";
    }

    public static class Suppliers
    {
        public const string View = "Suppliers.View";
        public const string Create = "Suppliers.Create";
        public const string Edit = "Suppliers.Edit";
        public const string Deactivate = "Suppliers.Deactivate";
    }

    public static class Purchases
    {
        public const string View = "Purchases.View";
        public const string Create = "Purchases.Create";
        public const string EditDraft = "Purchases.EditDraft";
        public const string Receive = "Purchases.Receive";
        public const string Cancel = "Purchases.Cancel";
        public const string Return = "Purchases.Return";
    }

    public static class SupplierPayments
    {
        public const string View = "SupplierPayments.View";
        public const string Register = "SupplierPayments.Register";
        public const string Refund = "SupplierPayments.Refund";
    }

    public static class SupplierBalances
    {
        public const string View = "SupplierBalances.View";
        public const string Adjust = "SupplierBalances.Adjust";
    }

    public static class Expenses
    {
        public const string View = "Expenses.View";
        public const string Create = "Expenses.Create";
        public const string Edit = "Expenses.Edit";
        public const string Approve = "Expenses.Approve";
    }

    public static class Reports
    {
        public const string Sales = "Reports.Sales";
        public const string Payments = "Reports.Payments";
        public const string Returns = "Reports.Returns";
        public const string Shifts = "Reports.Shifts";
        public const string Inventory = "Reports.Inventory";
        public const string Purchases = "Reports.Purchases";
        public const string SupplierBalances = "Reports.SupplierBalances";
        public const string Profit = "Reports.Profit";
        public const string Export = "Reports.Export";
    }

    public static class Terminals
    {
        public const string View = "Terminals.View";
        public const string Create = "Terminals.Create";
        public const string Edit = "Terminals.Edit";
        public const string Deactivate = "Terminals.Deactivate";
    }

    public static class Branches
    {
        public const string View = "Branches.View";
        public const string Manage = "Branches.Manage";
    }

    public static class Settings
    {
        public const string View = "Settings.View";
        public const string Manage = "Settings.Manage";
        public const string ManagePayments = "Settings.ManagePayments";
        public const string ManageReceipt = "Settings.ManageReceipt";
    }

    public static class Backups
    {
        public const string Create = "Backups.Create";
        public const string Restore = "Backups.Restore";
    }

    public static class AuditLogs
    {
        public const string View = "AuditLogs.View";
    }

    public static class Auth
    {
        public const string Login = "Auth.Login";
        public const string Logout = "Auth.Logout";
    }

    public static IReadOnlyCollection<(string Code, string Name, string Module)> All { get; } =
        new (string Code, string Name, string Module)[]
        {
            (Users.View, "عرض المستخدمين", "Users"),
            (Users.Create, "إنشاء مستخدم", "Users"),
            (Users.Edit, "تعديل المستخدم", "Users"),
            (Users.Deactivate, "تعطيل المستخدم", "Users"),
            (Users.ResetPassword, "إعادة تعيين كلمة المرور", "Users"),
            (Users.AssignRoles, "تعيين الأدوار", "Users"),
            (Users.ViewPermissions, "عرض صلاحيات المستخدم", "Users"),
            (Users.ManageOverrides, "إدارة الاستثناءات الفردية", "Users"),

            (Roles.View, "عرض الأدوار", "Roles"),
            (Roles.Create, "إنشاء دور", "Roles"),
            (Roles.Edit, "تعديل دور", "Roles"),
            (Roles.Deactivate, "تعطيل دور", "Roles"),
            (Roles.ManagePermissions, "إدارة صلاحيات الأدوار", "Roles"),
            (Employees.View, "عرض الموظفين", "Employees"),
            (Employees.Create, "إضافة موظف", "Employees"),
            (Employees.Edit, "تعديل بيانات موظف", "Employees"),
            (Employees.Deactivate, "تعطيل موظف", "Employees"),
            (Permissions.View, "عرض كتالوج الصلاحيات", "Permissions"),

            (Sales.View, "عرض المبيعات", "Sales"),
            (Sales.Create, "إنشاء عملية بيع", "Sales"),
            (Sales.AddItems, "إضافة أصناف للفاتورة", "Sales"),
            (Sales.ChangeQuantity, "تعديل الكمية", "Sales"),
            (Sales.ApplyDiscount, "تطبيق خصم عادي", "Sales"),
            (Sales.ApplyHighDiscount, "طلب خصم كبير", "Sales"),
            (Sales.RegisterPayment, "تسجيل دفعة", "Sales"),
            (Sales.RegisterSplitPayment, "تقسيم الدفع", "Sales"),
            (Sales.Complete, "إتمام البيع", "Sales"),
            (Sales.Cancel, "إلغاء بيع غير مكتمل", "Sales"),
            (Sales.CancelCompleted, "طلب إلغاء بيع مكتمل", "Sales"),
            (Sales.ViewDetails, "عرض تفاصيل الفاتورة", "Sales"),
            (Sales.ReprintReceipt, "إعادة طباعة الإيصال", "Sales"),
            (Sales.ChangePrice, "تغيير السعر داخل الفاتورة", "Sales"),
            (Sales.SellWithoutShift, "البيع بدون وردية", "Sales"),

            (Returns.View, "عرض المرتجعات", "Returns"),
            (Returns.Create, "إنشاء طلب مرتجع", "Returns"),
            (Returns.RefundCash, "رد مبلغ نقدي", "Returns"),
            (Returns.RefundToOriginalMethod, "رد المبلغ لطريقة الدفع الأصلية", "Returns"),
            (Returns.Approve, "اعتماد المرتجع", "Returns"),
            (Returns.WithoutReceipt, "مرتجع بدون فاتورة أصلية", "Returns"),
            (Returns.ViewHistory, "عرض تاريخ المرتجعات", "Returns"),

            (Shifts.ViewOwn, "عرض الوردية الخاصة", "Shifts"),
            (Shifts.ViewAll, "عرض جميع الورديات", "Shifts"),
            (Shifts.Open, "فتح وردية", "Shifts"),
            (Shifts.Close, "إغلاق وردية", "Shifts"),
            (Shifts.CountCash, "عد النقدية", "Shifts"),
            (Shifts.ViewExpectedCash, "عرض النقدية المتوقعة", "Shifts"),
            (Shifts.Reconcile, "تسوية الوردية", "Shifts"),
            (Shifts.ApproveDifference, "اعتماد فرق العهدة", "Shifts"),
            (Shifts.Reopen, "إعادة فتح وردية", "Shifts"),

            (CashMovements.ViewOwn, "عرض حركات العهدة الخاصة", "CashMovements"),
            (CashMovements.ViewAll, "عرض جميع حركات العهدة", "CashMovements"),
            (CashMovements.AddIn, "إضافة نقدية للعهدة", "CashMovements"),
            (CashMovements.AddOut, "سحب نقدية من العهدة", "CashMovements"),
            (CashMovements.Reverse, "عكس حركة نقدية", "CashMovements"),

            (Products.View, "عرض المنتجات", "Products"),
            (Products.Create, "إضافة منتج", "Products"),
            (Products.Edit, "تعديل منتج", "Products"),
            (Products.Deactivate, "تعطيل منتج", "Products"),
            (Products.ChangePrice, "تغيير سعر البيع", "Products"),
            (Products.ChangeCost, "تغيير التكلفة", "Products"),
            (Products.ManageBarcode, "إدارة الباركود", "Products"),

            (Categories.View, "عرض التصنيفات", "Categories"),
            (Categories.Create, "إنشاء تصنيف", "Categories"),
            (Categories.Edit, "تعديل تصنيف", "Categories"),
            (Categories.Deactivate, "تعطيل تصنيف", "Categories"),
            (Units.View, "عرض وحدات القياس", "Units"),
            (Units.Manage, "إدارة وحدات القياس", "Units"),

            (Inventory.ViewBalances, "عرض أرصدة المخزون", "Inventory"),
            (Inventory.ViewMovements, "عرض حركات المخزون", "Inventory"),
            (Inventory.ReceiveStock, "استلام مخزون", "Inventory"),
            (Inventory.AdjustStock, "إنشاء تسوية مخزون", "Inventory"),
            (Inventory.ApproveAdjustment, "اعتماد تسوية مخزون", "Inventory"),
            (Inventory.CountStock, "إجراء جرد", "Inventory"),
            (Inventory.ApproveCount, "اعتماد الجرد", "Inventory"),
            (Inventory.TransferStock, "تحويل مخزون", "Inventory"),
            (Inventory.ViewCost, "عرض تكلفة المخزون", "Inventory"),
            (Inventory.ViewValuation, "عرض قيمة المخزون", "Inventory"),
            (Inventory.Export, "تصدير المخزون", "Inventory"),

            (Suppliers.View, "عرض الموردين", "Suppliers"),
            (Suppliers.Create, "إضافة مورد", "Suppliers"),
            (Suppliers.Edit, "تعديل مورد", "Suppliers"),
            (Suppliers.Deactivate, "تعطيل مورد", "Suppliers"),

            (Purchases.View, "عرض المشتريات", "Purchases"),
            (Purchases.Create, "إنشاء فاتورة شراء", "Purchases"),
            (Purchases.EditDraft, "تعديل فاتورة شراء مسودة", "Purchases"),
            (Purchases.Receive, "استلام فاتورة شراء", "Purchases"),
            (Purchases.Cancel, "إلغاء فاتورة شراء", "Purchases"),
            (Purchases.Return, "إرجاع بضاعة للمورد", "Purchases"),

            (SupplierPayments.View, "عرض مدفوعات الموردين", "SupplierPayments"),
            (SupplierPayments.Register, "تسجيل دفعة لمورد", "SupplierPayments"),
            (SupplierPayments.Refund, "استرداد من مورد", "SupplierPayments"),
            (SupplierBalances.View, "عرض أرصدة الموردين", "SupplierBalances"),
            (SupplierBalances.Adjust, "تسوية رصيد مورد", "SupplierBalances"),

            (Expenses.View, "عرض المصروفات", "Expenses"),
            (Expenses.Create, "تسجيل مصروف", "Expenses"),
            (Expenses.Edit, "تعديل مصروف", "Expenses"),
            (Expenses.Approve, "اعتماد مصروف", "Expenses"),

            (Reports.Sales, "تقرير المبيعات", "Reports"),
            (Reports.Payments, "تقرير المدفوعات", "Reports"),
            (Reports.Returns, "تقرير المرتجعات", "Reports"),
            (Reports.Shifts, "تقرير الورديات", "Reports"),
            (Reports.Inventory, "تقرير المخزون", "Reports"),
            (Reports.Purchases, "تقرير المشتريات", "Reports"),
            (Reports.SupplierBalances, "تقرير أرصدة الموردين", "Reports"),
            (Reports.Profit, "تقرير الأرباح", "Reports"),
            (Reports.Export, "تصدير التقارير", "Reports"),

            (Terminals.View, "عرض أجهزة الكاشير", "Terminals"),
            (Terminals.Create, "إضافة جهاز كاشير", "Terminals"),
            (Terminals.Edit, "تعديل جهاز كاشير", "Terminals"),
            (Terminals.Deactivate, "تعطيل جهاز كاشير", "Terminals"),

            (Branches.View, "عرض الفروع", "Branches"),
            (Branches.Manage, "إدارة الفروع", "Branches"),

            (Settings.View, "عرض الإعدادات", "Settings"),
            (Settings.Manage, "تعديل الإعدادات", "Settings"),
            (Settings.ManagePayments, "إدارة وسائل الدفع", "Settings"),
            (Settings.ManageReceipt, "إعدادات الإيصال", "Settings"),
            (Backups.Create, "إنشاء نسخة احتياطية", "Backups"),
            (Backups.Restore, "استعادة نسخة احتياطية", "Backups"),

            (AuditLogs.View, "عرض سجل التدقيق", "AuditLogs"),
            (Auth.Login, "تسجيل الدخول", "Auth"),
            (Auth.Logout, "تسجيل الخروج", "Auth")
        };
}
