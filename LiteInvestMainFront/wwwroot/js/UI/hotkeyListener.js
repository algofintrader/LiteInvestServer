let DOTNET_JSINTEROPSERVICE_REFERENCE;

function setDonNetObjectReference(obj) {
    DOTNET_JSINTEROPSERVICE_REFERENCE = obj;
}

function keyDownHandler(e, windowId) {
        const windowElement = document.getElementById(windowId);
        console.log(e.code, windowElement);
    if (e.target && (e.target.nodeName != "INPUT") && e.target.nodeName != "TEXTAREA") {
        if (e.code !== "F5") {
            e.preventDefault();
        }
        DOTNET_JSINTEROPSERVICE_REFERENCE.invokeMethodAsync("KeyDown", e.code, e.ctrlKey, e.shiftKey);
    }
    else if (e.target && e.target.nodeName == "INPUT") {
        if (e.code == "Enter" || e.code == "Escape" || e.code == "NumpadEnter") {
            DOTNET_JSINTEROPSERVICE_REFERENCE.invokeMethodAsync("KeyDown", e.code, e.ctrlKey, e.shiftKey);
        }
    }
}

function keyUpHandler(e) {
    //if (e.target && (e.target.nodeName != "INPUT")) {
    //    DOTNET_JSINTEROPSERVICE_REFERENCE.invokeMethodAsync("KeyUp", e.code, e.ctrlKey, e.shiftKey);
    //}
} 

window.addHotkeyListener = (windowId) => {
    const windowElement = document.getElementById(windowId);
    if (!windowElement) return;
    windowElement.addEventListener('keydown', (e) => {
        keyDownHandler(e, windowId);
    });
};