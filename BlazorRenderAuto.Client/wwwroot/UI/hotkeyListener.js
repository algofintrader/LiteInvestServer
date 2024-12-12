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

window.focusElementById = (windowId) => {
    const element = document.getElementById(windowId);
    if (element) {
        console.log(element);
        element.focus(); // Устанавливаем фокус на элемент
    }
};

function getScrollEvent(gridTableId) {
    let parent = document.getElementById(gridTableId);
    if (parent) {
        let targetElement = parent.querySelector(".k-grid-content");
        if (targetElement) {
            targetElement.addEventListener('scroll', (event) => {
                if (parent) {
                    let visibleHeight = targetElement.clientHeight;

                    let headerHeight = 0;
                    let header = document.querySelector("#header");
                    if (header) {
                        headerHeight = header.offsetHeight + 20;
                    }

                    let rows = targetElement.querySelectorAll("tr");

                    let firstVisibleRowPrice = null;
                    let lastVisibleRowPrice = null;
                    let visibleRowCount = 0;

                    rows.forEach((row, index) => {
                        let rect = row.getBoundingClientRect();

                        if (rect.top - headerHeight < visibleHeight && rect.bottom - headerHeight > 0) {
                            let priceCell = row.querySelector(".price");

                            if (priceCell) {
                                let price = priceCell.textContent.trim();

                                if (firstVisibleRowPrice === null) {
                                    firstVisibleRowPrice = price;
                                }
                                lastVisibleRowPrice = price;

                                visibleRowCount++;
                            }
                        }
                    });

                    if (firstVisibleRowPrice !== null && lastVisibleRowPrice !== null) {
                        DOTNET_JSINTEROPSERVICE_REFERENCE.invokeMethodAsync(
                            "OnScroll", gridTableId,
                            firstVisibleRowPrice,
                            lastVisibleRowPrice,
                            visibleRowCount
                        );
                    }
                } else {
                    console.log("Элемент с ID " + gridTableId + " не найден");
                }
            });
        }
    } else {
        console.log("Родительский элемент с ID " + gridTableId + " не найден");
    }
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

//function scrollToRow(gridItemId, rowIndex) {
//    let parent = document.getElementById(gridItemId);
//    if (parent) {
//        let scrollableElement = parent.querySelector(".k-grid-content");
//        if (scrollableElement) {
//            let row = scrollableElement.querySelectorAll(".k-table-tbody tr")[rowIndex];
//            if (row) {
//                row.classList.add("row");
//                row.scrollIntoView({ behavior: 'smooth', block: 'center' });
//            }
//        }
//    }
//}

function scrollToRow(gridItemId, rowIndex) {
    let parent = document.getElementById(gridItemId);
    if (parent) {
        let scrollableElement = parent.querySelector(".k-grid-content");
        if (scrollableElement) {
            let rows = scrollableElement.querySelectorAll(".k-table-tbody tr");

            // Если строки с таким индексом ещё не загружены, прокручиваем до нужного места
            if (rows.length <= rowIndex) {
                let rowHeight = rows.length > 0 ? rows[0].offsetHeight : 30; // Примерная высота строки
                let targetScrollTop = rowIndex * rowHeight;

                // Прокрутка до нужного места
                scrollableElement.scrollTop = targetScrollTop;

                // Нужно подождать, чтобы новые строки загрузились в DOM, и затем снова проверим
                setTimeout(function () {
                    let rowsAfterScroll = scrollableElement.querySelectorAll(".k-table-tbody tr");
                    if (rowsAfterScroll.length > rowIndex) {
                        let targetRow = rowsAfterScroll[rowIndex];
                        // Добавляем желтый фон
                        targetRow.classList.add("highlight-row");
                        targetRow.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }
                }, 300); // Задержка для подгрузки строк
            } else {
                // Если строки уже в DOM, прокручиваем сразу
                let targetRow = rows[rowIndex];
                // Добавляем желтый фон
                targetRow.classList.add("highlight-row");
                targetRow.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }
    }
}
