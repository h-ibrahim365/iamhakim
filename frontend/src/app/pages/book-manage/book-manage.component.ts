import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { TranslationService } from '../../i18n/translation.service';
import { ApiService } from '../../core/api.service';
import { AvailabilityDay, AvailabilitySlot, BookingView, MeetingKind } from '../../core/contracts';

@Component({
  selector: 'app-book-manage',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './book-manage.component.html',
  styleUrl: './book-manage.component.scss'
})
export class BookManageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ApiService);
  private readonly i18n = inject(TranslationService);

  protected readonly loading = signal(true);
  protected readonly actionBusy = signal(false);
  protected readonly confirmCancel = signal(false);
  protected readonly rescheduleOpen = signal(false);
  protected readonly availabilityLoading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly actionMessage = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  protected readonly booking = signal<BookingView | null>(null);
  protected readonly days = signal<AvailabilityDay[]>([]);
  protected readonly selectedRescheduleSlot = signal<AvailabilitySlot | null>(null);

  private token = '';

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (!this.token) {
      this.loading.set(false);
      this.error.set(this.i18n.t('book.manage.error.missingToken'));
      return;
    }

    this.loadBooking();
  }

  canCancel(): boolean {
    const status = this.booking()?.status;
    return status === 'pending'
      || status === 'accepted'
      || status === 'reschedule_requested'
      || status === 'confirmed'
      || status === 'rescheduled';
  }

  canReschedule(): boolean {
    return this.canCancel();
  }

  askCancel(): void {
    this.actionMessage.set(null);
    this.actionError.set(null);
    this.confirmCancel.set(true);
  }

  keepBooking(): void {
    this.confirmCancel.set(false);
  }

  openReschedule(): void {
    this.confirmCancel.set(false);
    this.actionMessage.set(null);
    this.actionError.set(null);
    this.rescheduleOpen.set(true);
    this.selectedRescheduleSlot.set(null);

    if (this.days().length === 0) {
      this.loadAvailability();
    }
  }

  closeReschedule(): void {
    this.rescheduleOpen.set(false);
    this.selectedRescheduleSlot.set(null);
  }

  selectRescheduleSlot(slot: AvailabilitySlot): void {
    if (!slot.available) return;
    this.selectedRescheduleSlot.set(slot);
  }

  rescheduleBooking(): void {
    const slot = this.selectedRescheduleSlot();
    if (!this.token || !slot || !this.canReschedule() || this.actionBusy()) return;

    this.actionBusy.set(true);
    this.actionError.set(null);
    this.actionMessage.set(null);

    this.api.manageBooking(this.token, 'reschedule', slot.id).subscribe({
      next: () => {
        this.rescheduleOpen.set(false);
        this.selectedRescheduleSlot.set(null);
        this.actionMessage.set(this.i18n.t('book.manage.reschedule.success'));
        this.loadBooking(false);
        this.loadAvailability();
      },
      error: (err) => {
        this.actionError.set(this.translateApiError(err, 'book.manage.reschedule.error'));
        this.actionBusy.set(false);
      }
    });
  }

  cancelBooking(): void {
    if (!this.token || !this.canCancel() || this.actionBusy()) return;

    this.actionBusy.set(true);
    this.actionError.set(null);
    this.actionMessage.set(null);

    this.api.manageBooking(this.token, 'cancel').subscribe({
      next: () => {
        this.confirmCancel.set(false);
        this.rescheduleOpen.set(false);
        this.actionMessage.set(this.i18n.t('book.manage.cancel.success'));
        this.loadBooking(false);
      },
      error: (err) => {
        this.actionError.set(this.translateApiError(err, 'book.manage.cancel.error'));
        this.actionBusy.set(false);
      }
    });
  }

  kindLabel(kind: MeetingKind): string {
    const map: Record<MeetingKind, string> = {
      Video: 'book.kind.video',
      Call: 'book.kind.call',
      InPerson: 'book.kind.inperson'
    };
    return this.i18n.t(map[kind]);
  }

  statusLabel(status: string): string {
    const key = `book.status.${status}`;
    const translated = this.i18n.t(key);
    return translated === key ? status : translated;
  }

  fullWhen(iso: string): string {
    return this.formatDate(new Date(iso), {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  dayLabel(day: AvailabilityDay): string {
    return this.formatDate(new Date(day.date + 'T00:00:00'), {
      weekday: 'long',
      day: 'numeric',
      month: 'long'
    });
  }

  slotTime(slot: AvailabilitySlot): string {
    return new Date(slot.startUtc).toLocaleTimeString(this.locale(), { hour: '2-digit', minute: '2-digit' });
  }

  visibleDays(): AvailabilityDay[] {
    return this.days()
      .map((day) => ({ ...day, slots: day.slots.filter((slot) => slot.available) }))
      .filter((day) => day.slots.length > 0)
      .slice(0, 10);
  }

  private loadBooking(showLoading = true): void {
    if (showLoading) this.loading.set(true);

    this.api.getBooking(this.token).subscribe({
      next: (booking) => {
        this.booking.set(booking);
        this.loading.set(false);
        this.actionBusy.set(false);
      },
      error: () => {
        this.error.set(this.i18n.t('book.manage.error.notFound'));
        this.loading.set(false);
        this.actionBusy.set(false);
      }
    });
  }

  private loadAvailability(): void {
    this.availabilityLoading.set(true);
    this.api.getAvailability().subscribe({
      next: (res) => {
        this.days.set(res.days);
        this.availabilityLoading.set(false);
      },
      error: () => {
        this.actionError.set(this.i18n.t('book.manage.reschedule.availabilityError'));
        this.availabilityLoading.set(false);
      }
    });
  }

  private translateApiError(error: unknown, fallbackKey: string): string {
    const code = this.apiErrorCode(error);
    if (!code) return this.i18n.t(fallbackKey);

    const key = `book.apiError.${code}`;
    const translated = this.i18n.t(key);
    return translated === key ? this.i18n.t(fallbackKey) : translated;
  }

  private apiErrorCode(error: unknown): string | null {
    if (typeof error !== 'object' || error === null || !('error' in error)) return null;

    const payload = (error as { error?: unknown }).error;
    if (typeof payload !== 'object' || payload === null || !('code' in payload)) return null;

    const code = (payload as { code?: unknown }).code;
    return typeof code === 'string' ? code : null;
  }

  private formatDate(date: Date, options: Intl.DateTimeFormatOptions): string {
    return new Intl.DateTimeFormat(this.locale(), options)
      .formatToParts(date)
      .map((part) => ['weekday', 'month'].includes(part.type) ? this.capitalizeFirst(part.value) : part.value)
      .join('');
  }

  private capitalizeFirst(value: string): string {
    return value.length === 0
      ? value
      : value.charAt(0).toLocaleUpperCase(this.locale()) + value.slice(1);
  }

  private locale(): string {
    switch (this.i18n.lang()) {
      case 'fr': return 'fr-BE';
      case 'nl': return 'nl-BE';
      default: return 'en-GB';
    }
  }
}
