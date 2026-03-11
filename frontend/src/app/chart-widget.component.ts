import {
  Component, Input, OnChanges, OnDestroy, AfterViewInit,
  ElementRef, ViewChild, ChangeDetectionStrategy,
} from "@angular/core";
import { Chart, ChartType, registerables } from "chart.js";

Chart.register(...registerables);

@Component({
  selector: "pm-chart-widget",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<canvas #canvas></canvas>`,
  styles: [
    `:host { display: block; width: 100%; height: 240px; }
     canvas { width: 100% !important; height: 100% !important; }`,
  ],
})
export class ChartWidgetComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() chartType: ChartType = "bar";
  @Input() chartData: unknown = null;

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
    this.chart?.destroy();
    if (!this.chartData || !this.canvasRef?.nativeElement) return;

    const isRound = this.chartType === "pie" || this.chartType === "doughnut";
    const isScatter = this.chartType === "scatter";

    this.chart = new Chart(this.canvasRef.nativeElement, {
      type: this.chartType,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      data: this.chartData as any,
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 300 },
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
