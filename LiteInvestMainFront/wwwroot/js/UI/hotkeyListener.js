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
        DOTNET_JSINTEROPSERVICE_REFERENCE.invokeMethodAsync("KeyDown", windowId, e.code, e.ctrlKey, e.shiftKey);
    }
    else if (e.target && e.target.nodeName == "INPUT") {
        if (e.code == "Enter" || e.code == "Escape" || e.code == "NumpadEnter") {
            DOTNET_JSINTEROPSERVICE_REFERENCE.invokeMethodAsync("KeyDown", windowId, e.code, e.ctrlKey, e.shiftKey);
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

function getScrollEvent(gridTableId) {
    //setTimeout(() => {
    //    let parent = document.getElementById(gridTableId);
    //    if (parent) {
    //        let targetElement = parent.querySelector(".k-grid-content");
    //        if (targetElement) {
    //            targetElement.addEventListener('scroll', (event) => {

    //                if (parent) {
    //                    DOTNET_JSINTEROPSERVICE_REFERENCE.invokeMethodAsync("OnScroll", gridTableId);
    //                } else {
    //                    console.log("Элемент с ID " + gridTableId + " не найден");
    //                }
    //            });
    //        }
    //    } else {
    //        console.log("Родительский элемент с ID " + gridTableId + " не найден");
    //    }
    //}, 2000);
}

function getWheelEvent(historyTableId) {
    let parent = document.getElementById(historyTableId);
    if (parent) {
        let targetElement = parent.querySelector(".k-grid-content");
        if (targetElement) {
            targetElement.addEventListener('wheel', (event) => {

                if (parent) {
                    DOTNET_JSINTEROPSERVICE_REFERENCE.invokeMethodAsync("OnWheel", historyTableId);

                    const isZoomIn = event.deltaY < 0; // Скролл вверх (+) — увеличение
                    const isZoomOut = event.deltaY > 0; // Скролл вниз (-) — уменьшение

                    // Задаем шаг изменения ширины
                    const zoomStep = 30; // Шаг изменения ширины в пикселях

                    const columns = targetElement.querySelectorAll('th, td');

                    columns.forEach((column, index) => {
                        const currentWidth = parseInt(window.getComputedStyle(column).width, 10);

                        // Если скроллим вверх (увеличение), увеличиваем ширину
                        if (isZoomIn && currentWidth < 300) {
                            column.style.width = `${currentWidth + zoomStep}px`;
                        }

                        // Если скроллим вниз (уменьшение), уменьшаем ширину
                        if (isZoomOut && currentWidth > 50) {
                            column.style.width = `${currentWidth - zoomStep}px`;
                        }

                    })
                }
                else {
                    console.log("Элемент с ID " + gridTableId + " не найден");
                }
            });
        }
    } else {
        console.log("Родительский элемент с ID " + gridTableId + " не найден");
    };
}