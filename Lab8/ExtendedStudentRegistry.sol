// SPDX-License-Identifier: MIT
pragma solidity ^0.8.19;

import "./StudentRegistry.sol";

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
