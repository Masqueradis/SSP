// SPDX-License-Identifier: MIT
pragma solidity ^0.8.19;

contract StudentRegistry {
    // ============================================================
    // DATA STRUCTURES
    // ============================================================
    
    struct Student {
        string name;
        uint256 age;
        string major;
        bool isActive;
    }
    
    // ============================================================
    // STATE VARIABLES
    // ============================================================
    
    address public admin;
    string public universityName;
    uint256 public studentCount;
    
    mapping(uint256 => Student) public students;
    mapping(address => uint256) public studentIdByAddress;
    
    // ============================================================
    // EVENTS
    // ============================================================
    
    event StudentRegistered(uint256 indexed studentId, string name);
    event StudentUpdated(uint256 indexed studentId, string name);
    event StudentDeleted(uint256 indexed studentId);
    
    // ============================================================
    // MODIFIERS
    // ============================================================
    
    modifier onlyAdmin() {
        require(msg.sender == admin, "Only admin can call this");
        _;
    }
    
    modifier studentExists(uint256 _studentId) {
        require(students[_studentId].isActive, "Student does not exist");
        _;
    }
    
    // ============================================================
    // CONSTRUCTOR
    // ============================================================
    
    constructor(string memory _universityName) {
        admin = msg.sender;
        universityName = _universityName;
        studentCount = 0;
    }
    
    // ============================================================
    // CORE FUNCTIONS
    // ============================================================
    
    function registerStudent(
        string memory _name,
        uint256 _age,
        string memory _major
    ) public onlyAdmin {
        require(bytes(_name).length > 0, "Name cannot be empty");
        require(_age > 0 && _age < 150, "Invalid age");
        require(bytes(_major).length > 0, "Major cannot be empty");
        
        studentCount++;
        
        students[studentCount] = Student({
            name: _name,
            age: _age,
            major: _major,
            isActive: true
        });
        
        studentIdByAddress[msg.sender] = studentCount;
        
        emit StudentRegistered(studentCount, _name);
    }
    
    function updateStudent(
        uint256 _studentId,
        string memory _newName,
        uint256 _newAge,
        string memory _newMajor
    ) public onlyAdmin studentExists(_studentId) {
        require(bytes(_newName).length > 0, "Name cannot be empty");
        require(_newAge > 0 && _newAge < 150, "Invalid age");
        require(bytes(_newMajor).length > 0, "Major cannot be empty");
        
        students[_studentId].name = _newName;
        students[_studentId].age = _newAge;
        students[_studentId].major = _newMajor;
        
        emit StudentUpdated(_studentId, _newName);
    }
    
    function deleteStudent(uint256 _studentId) public onlyAdmin studentExists(_studentId) {
        students[_studentId].isActive = false;
        emit StudentDeleted(_studentId);
    }
    
    // ============================================================
    // VIEW FUNCTIONS (READ ONLY)
    // ============================================================
    
    function getStudent(uint256 _studentId)
        public
        view
        studentExists(_studentId)
        returns (string memory, uint256, string memory, bool)
    {
        Student memory s = students[_studentId];
        return (s.name, s.age, s.major, s.isActive);
    }
    
    function getStudentByAddress(address _address)
        public
        view
        returns (uint256, string memory, uint256, string memory, bool)
    {
        uint256 id = studentIdByAddress[_address];
        require(id > 0 && students[id].isActive, "Student not found");
        
        Student memory s = students[id];
        return (id, s.name, s.age, s.major, s.isActive);
    }
    
    function getAllStudents()
        public
        view
        returns (uint256[] memory, string[] memory, uint256[] memory, string[] memory)
    {
        uint256 activeCount = 0;
        
        // First count active students
        for (uint256 i = 1; i <= studentCount; i++) {
            if (students[i].isActive) {
                activeCount++;
            }
        }
        
        // Then fill arrays
        uint256[] memory ids = new uint256[](activeCount);
        string[] memory names = new string[](activeCount);
        uint256[] memory ages = new uint256[](activeCount);
        string[] memory majors = new string[](activeCount);
        
        uint256 index = 0;
        for (uint256 i = 1; i <= studentCount; i++) {
            if (students[i].isActive) {
                ids[index] = i;
                names[index] = students[i].name;
                ages[index] = students[i].age;
                majors[index] = students[i].major;
                index++;
            }
        }
        
        return (ids, names, ages, majors);
    }
    
    function getStudentCount() public view returns (uint256) {
        uint256 count = 0;
        for (uint256 i = 1; i <= studentCount; i++) {
            if (students[i].isActive) {
                count++;
            }
        }
        return count;
    }
    
    function isStudentRegistered(address _address) public view returns (bool) {
        uint256 id = studentIdByAddress[_address];
        return id > 0 && students[id].isActive;
    }
    
    // ============================================================
    // ADMIN FUNCTIONS
    // ============================================================
    
    function changeAdmin(address _newAdmin) public onlyAdmin {
        require(_newAdmin != address(0), "Invalid address");
        admin = _newAdmin;
    }
    
    function setUniversityName(string memory _newName) public onlyAdmin {
        require(bytes(_newName).length > 0, "Name cannot be empty");
        universityName = _newName;
    }
}

// ============================================================
// EXTRA: Simple contract to demonstrate inheritance
// ============================================================

contract ExtendedStudentRegistry is StudentRegistry {
    string public department;
    
    constructor(string memory _universityName, string memory _department)
        StudentRegistry(_universityName)
    {
        department = _department;
    }
    
    function setDepartment(string memory _newDepartment) public onlyAdmin {
        department = _newDepartment;
    }
    
    function getStudentWithMajor(uint256 _studentId, string memory _major)
        public
        view
        studentExists(_studentId)
        returns (bool)
    {
        return keccak256(bytes(students[_studentId].major)) == keccak256(bytes(_major));
    }
}
