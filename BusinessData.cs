using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackInBusiness
{
    /// <summary>
    /// Class to store business data including employee count and floor information
    /// </summary>
    public class BusinessInfo
    {
        public NewAddress Address { get; set; }
        public int EmployeeCount { get; set; }
        public string FloorName { get; set; }
        
        public BusinessInfo(NewAddress address, int employeeCount, string floorName)
        {
            Address = address;
            EmployeeCount = employeeCount;
            FloorName = floorName;
        }
    }
}
