namespace SRDCS.wwwroot.js
{
    public class sacco
    {
    }
}
// wwwroot/js/sacco.js
window.saveSACCO = async function (sacco) {
    const response = await fetch('/SACCO/Save', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify(sacco)
    });
    return await response.json();
};

window.updateSACCO = async function (sacco) {
    const response = await fetch('/SACCO/Update', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        },
        body: JSON.stringify(sacco)
    });
    return await response.json();
};

window.deleteSACCO = async function (id) {
    const response = await fetch(`/SACCO/Delete/${id}`, {
        method: 'POST',
        headers: {
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
        }
    });
    return await response.json();
};

window.getFilteredSACCOs = async function (filter) {
    const response = await fetch('/SACCO/GetFiltered', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(filter)
    });
    return await response.json();
};