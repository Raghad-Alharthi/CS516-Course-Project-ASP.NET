namespace StudentManagementSystem.Models.ViewModels{
    public class ManageClassesViewModel
    {
        public List<Class> Classes { get; set; }
        public Class ClassToEdit { get; set; }
        public Dictionary<int, DateTime?> FirstLecturesByClassId { get; set; } = new();
    }
}