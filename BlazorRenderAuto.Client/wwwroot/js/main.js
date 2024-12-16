window.focusElementById = (windowId) => {
    const element = document.getElementById(windowId);
    if (element) {
        console.log(element);
        element.focus({ preventScroll: true });
    }
};

function getScrollEventForAllTables(gridTableId) {
    let parent = document.getElementById(gridTableId);
    if (parent) {
        console.log(parent);
        parent.addEventListener("scroll", (e) => {
            if (e.target && e.target.classList.contains("k-grid-content")) {
                let targetElements = parent.querySelectorAll(".k-grid-content");
                let scrollTop = e.target.scrollTop;

                targetElements.forEach((otherElement) => {
                    if (otherElement !== e.target) {
                        console.log(otherElement);
                        otherElement.scrollTop = scrollTop;
                    }
                });
            }
        }, true);
    }
}

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