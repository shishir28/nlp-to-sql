import {
  Component, Input, Output, EventEmitter, OnChanges, OnDestroy, AfterViewInit,
  ElementRef, ViewChild, ChangeDetectionStrategy,
} from "@angular/core";
import { Chart, ChartType, registerables } from "chart.js";

Chart.register(...registerables);

@Component({
  selector: "pm-chart-widget",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div style="position:relative;width:100%;height:240px"><canvas #canvas></canvas></div>`,
  styles: [],
})
export class ChartWidgetComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() chartType: ChartType = "bar";
  @Input() chartData: unknown = null;
  @Output() barClick = new EventEmitter<string>();

  @ViewChild("canvas") canvasRef!: ElementRef<HTMLCanvasElement>;

  private chart?: Chart;
  private ready = false;

  ngAfterViewInit(): void {
    this.ready = true;
    this.render();
  }

  ngOnChanges(): void {
    if (this.ready) this.render();
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  private render(): void {
    const canvas = this.canvasRef?.nativeElement;
    if (!this.chartData || !canvas) {
      this.chart?.destroy();
      this.chart = undefined;
      return;
    }

    // Destroy and recreate only when chart type changes
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    if (this.chart && (this.chart.config as any).type !== this.chartType) {
      this.chart.destroy();
      this.chart = undefined;
    }

    // Update data in place to avoid rAF cancellation flicker
    if (this.chart) {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      this.chart.data = this.chartData as any;
      this.chart.update('none');
      return;
    }

    const isRound = this.chartType === "pie" || this.chartType === "doughnut";
    const isScatter = this.chartType === "scatter";

    this.chart = new Chart(canvas, {
      type: this.chartType,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      data: this.chartData as any,
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 300 },
        onClick: (_event, elements) => {
          if (!elements.length) return;
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          const label = (this.chart?.data?.labels as any[])?.[elements[0].index];
          if (label != null) this.barClick.emit(String(label));
        },
        plugins: {
          legend: {
            display: isRound,
            position: "bottom",
            labels: { font: { size: 11 }, boxWidth: 12 },
          },
          tooltip: { bodyFont: { size: 11 }, titleFont: { size: 11 } },
        },
        ...(isRound || isScatter ? {} : {
          scales: {
            x: { ticks: { font: { size: 10 }, maxRotation: 35 } },
            y: { ticks: { font: { size: 10 } }, beginAtZero: true },
          },
        }),
        ...(isScatter ? {
          scales: {
            x: { type: "linear", position: "bottom", ticks: { font: { size: 10 } } },
            y: { ticks: { font: { size: 10 } } },
          },
        } : {}),
      },
    });
  }
}
