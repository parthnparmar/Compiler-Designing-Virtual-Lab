$(document).ready(function () {
    // Sidebar Toggle
    $('#sidebarCollapse').on('click', function () {
        $('#sidebar').toggleClass('active');
        $('#content').toggleClass('active');
    });

    // Theme Toggle
    const themeToggle = $('#themeToggle');
    const html = $('html');
    
    // Load saved theme
    const savedTheme = localStorage.getItem('theme') || 'light';
    html.attr('data-theme', savedTheme);
    updateThemeIcon(savedTheme);

    themeToggle.on('click', function () {
        const currentTheme = html.attr('data-theme');
        const newTheme = currentTheme === 'light' ? 'dark' : 'light';
        html.attr('data-theme', newTheme);
        localStorage.setItem('theme', newTheme);
        updateThemeIcon(newTheme);
    });

    function updateThemeIcon(theme) {
        const icon = themeToggle.find('i');
        if (theme === 'dark') {
            icon.removeClass('fa-moon').addClass('fa-sun');
        } else {
            icon.removeClass('fa-sun').addClass('fa-moon');
        }
    }

    // Responsive Sidebar
    if ($(window).width() < 768) {
        $('#sidebar').addClass('active');
    }

    $(window).resize(function () {
        if ($(window).width() < 768) {
            $('#sidebar').addClass('active');
            $('#content').removeClass('active');
        } else {
            $('#sidebar').removeClass('active');
            $('#content').removeClass('active');
        }
    });

    // Smooth Scroll
    $('a[href^="#"]').on('click', function (e) {
        e.preventDefault();
        const target = $(this.getAttribute('href'));
        if (target.length) {
            $('html, body').stop().animate({
                scrollTop: target.offset().top - 100
            }, 1000);
        }
    });

    // Form Animation
    $('.form-control, .form-select').on('focus', function () {
        $(this).parent().addClass('focused');
    }).on('blur', function () {
        $(this).parent().removeClass('focused');
    });
});

// Export to PDF Function
function exportToPDF() {
    const { jsPDF } = window.jspdf;
    const doc = new jsPDF();
    
    const content = document.querySelector('.result-box') || document.querySelector('.container-fluid');
    
    html2canvas(content, {
        scale: 2,
        useCORS: true,
        logging: false
    }).then(canvas => {
        const imgData = canvas.toDataURL('image/png');
        const imgWidth = 190;
        const pageHeight = 290;
        const imgHeight = (canvas.height * imgWidth) / canvas.width;
        let heightLeft = imgHeight;
        let position = 10;

        doc.addImage(imgData, 'PNG', 10, position, imgWidth, imgHeight);
        heightLeft -= pageHeight;

        while (heightLeft >= 0) {
            position = heightLeft - imgHeight + 10;
            doc.addPage();
            doc.addImage(imgData, 'PNG', 10, position, imgWidth, imgHeight);
            heightLeft -= pageHeight;
        }

        doc.save('compiler-lab-result.pdf');
    });
}

// Download SVG Function
function downloadSVG(svgElement, filename) {
    const svg = svgElement.outerHTML;
    const blob = new Blob([svg], { type: 'image/svg+xml' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename || 'diagram.svg';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
}

// Download PNG Function
function downloadPNG(svgElement, filename) {
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d');
    const svg = svgElement.outerHTML;
    const img = new Image();
    
    const svgBlob = new Blob([svg], { type: 'image/svg+xml;charset=utf-8' });
    const url = URL.createObjectURL(svgBlob);
    
    img.onload = function () {
        canvas.width = img.width;
        canvas.height = img.height;
        ctx.drawImage(img, 0, 0);
        
        canvas.toBlob(function (blob) {
            const pngUrl = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = pngUrl;
            link.download = filename || 'diagram.png';
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(pngUrl);
        });
        
        URL.revokeObjectURL(url);
    };
    
    img.src = url;
}

// Add download buttons to diagrams
$(document).ready(function () {
    $('.diagram-container svg').each(function (index) {
        const svg = this;
        const container = $(this).parent();
        
        if (container.find('.diagram-controls').length === 0) {
            const controls = $(`
                <div class="diagram-controls mt-3">
                    <button class="btn btn-sm btn-primary me-2" onclick="downloadSVG(this.closest('.diagram-container').querySelector('svg'), 'diagram-${index}.svg')">
                        <i class="fas fa-download"></i> SVG
                    </button>
                    <button class="btn btn-sm btn-success" onclick="downloadPNG(this.closest('.diagram-container').querySelector('svg'), 'diagram-${index}.png')">
                        <i class="fas fa-download"></i> PNG
                    </button>
                </div>
            `);
            container.append(controls);
        }
    });
});
