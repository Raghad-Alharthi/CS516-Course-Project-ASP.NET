using System;
using System.Collections.Generic;

namespace StudentManagementSystem.Models;

public partial class StudentClass
{
    public int StudentClassID { get; set; }

    public int StudentID { get; set; }

    public int ClassID { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
