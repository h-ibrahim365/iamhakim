import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  PLATFORM_ID,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { TranslationService } from '../../i18n/translation.service';
import { ApiService } from '../../core/api.service';
import {
  AddressSuggestion,
  AvailabilityDay,
  AvailabilitySlot,
  BookingResult,
  MeetingKind,
} from '../../core/contracts';

interface CalCell {
  date: string | null; // yyyy-MM-dd, or null for padding cells
  day: number | null;
  inMonth: boolean;
  available: number; // count of free slots
  total: number; // count of slots exposed for that day
  state: 'none' | 'available' | 'partial';
  isToday: boolean;
  isPast: boolean;
}

interface TurnstileRenderOptions {
  sitekey: string;
  theme: 'auto' | 'dark' | 'light';
  callback: (token: string) => void;
  'expired-callback': () => void;
  'error-callback': () => void;
  size?: 'normal' | 'compact' | 'flexible';
}

interface TurnstileApi {
  render: (container: HTMLElement, options: TurnstileRenderOptions) => string;
  reset: (widgetId?: string) => void;
  remove?: (widgetId: string) => void;
}

declare global {
  interface Window {
    turnstile?: TurnstileApi;
  }
}

@Component({
  selector: 'app-book',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './book.component.html',
  styleUrl: './book.component.scss',
})
export class BookComponent implements OnInit, AfterViewInit, OnDestroy {
  private static readonly emailResendCooldownSeconds = 60;

  @ViewChild('turnstileContainer')
  private set turnstileContainerRef(value: ElementRef<HTMLDivElement> | undefined) {
    this.turnstileContainer = value;

    // The Turnstile container is inside an async template branch. ngAfterViewInit can
    // run while the loading branch is still displayed, so render again as soon as the
    // real container appears.
    if (value) {
      queueMicrotask(() => this.renderTurnstileIfNeeded());
    }
  }

  private turnstileContainer?: ElementRef<HTMLDivElement>;
  private readonly api = inject(ApiService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly i18n = inject(TranslationService);

  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly timeZone = signal('Europe/Brussels');

  /** availability keyed by yyyy-MM-dd for quick lookup */
  private readonly dayMap = signal<Map<string, AvailabilityDay>>(new Map());

  /** first day of the month currently shown */
  protected readonly viewMonth = signal<Date>(this.firstOfMonth(new Date()));
  protected readonly selectedDate = signal<string | null>(null);
  protected readonly selectedSlot = signal<AvailabilitySlot | null>(null);

  protected readonly weekdayLabelKeys = [
    'book.weekday.mon',
    'book.weekday.tue',
    'book.weekday.wed',
    'book.weekday.thu',
    'book.weekday.fri',
    'book.weekday.sat',
    'book.weekday.sun',
  ];

  // form
  protected readonly kind = signal<MeetingKind>('Video');
  protected name = '';
  protected email = '';
  protected emailCode = '';
  protected message = '';
  protected meetingLocation = '';
  protected readonly addressSuggestions = signal<AddressSuggestion[]>([]);
  protected readonly addressSearchBusy = signal(false);
  protected readonly addressSearchError = signal(false);
  private readonly addressSuggestionChosen = signal(false);

  private addressSearchTimer: number | null = null;
  private addressSearchSequence = 0;

  protected readonly emailVerificationId = signal<string | null>(null);
  protected readonly verificationEmail = signal<string | null>(null);
  protected readonly emailVerificationToken = signal<string | null>(null);
  protected readonly verifiedEmail = signal<string | null>(null);
  protected readonly emailVerificationBusy = signal(false);
  protected readonly emailVerificationError = signal<string | null>(null);
  protected readonly emailVerificationInfo = signal<string | null>(null);
  protected readonly emailResendCooldownSeconds = signal(0);

  protected readonly turnstileEnabled = signal(false);
  protected readonly turnstileToken = signal<string | null>(null);
  protected readonly turnstileLoading = signal(false);
  protected readonly turnstileError = signal<string | null>(null);

  private turnstileSiteKey = '';
  private turnstileWidgetId: string | null = null;
  private turnstileScriptPromise: Promise<void> | null = null;
  private turnstileViewReady = false;

  protected readonly submitting = signal(false);
  protected readonly submitError = signal<string | null>(null);
  protected readonly result = signal<BookingResult | null>(null);
  protected readonly copied = signal(false);

  protected readonly meetingKinds: { value: MeetingKind; labelKey: string; icon: string }[] = [
    { value: 'Video', labelKey: 'book.kind.video', icon: '🎥' },
    { value: 'Call', labelKey: 'book.kind.call', icon: '📞' },
    { value: 'InPerson', labelKey: 'book.kind.inperson', icon: '🤝' },
  ];

  protected readonly monthLabel = computed(() =>
    this.formatDate(this.viewMonth(), { month: 'long', year: 'numeric' }, ['month']),
  );

  /** the 6-row grid of cells for the visible month */
  protected readonly cells = computed<CalCell[]>(() => {
    const month = this.viewMonth();
    const map = this.dayMap();
    const year = month.getFullYear();
    const m = month.getMonth();

    const first = new Date(year, m, 1);
    // Monday-based offset
    const startOffset = (first.getDay() + 6) % 7;
    const daysInMonth = new Date(year, m + 1, 0).getDate();

    const todayStr = this.toKey(new Date());
    const cells: CalCell[] = [];

    for (let i = 0; i < startOffset; i++) {
      cells.push({
        date: null,
        day: null,
        inMonth: false,
        available: 0,
        total: 0,
        state: 'none',
        isToday: false,
        isPast: false,
      });
    }
    for (let d = 1; d <= daysInMonth; d++) {
      const date = new Date(year, m, d);
      const key = this.toKey(date);
      const avail = map.get(key);
      const totalCount = avail?.slots.length ?? 0;
      const freeCount = avail ? avail.slots.filter((s) => s.available).length : 0;
      const state = freeCount === 0 ? 'none' : freeCount === totalCount ? 'available' : 'partial';

      cells.push({
        date: key,
        day: d,
        inMonth: true,
        available: freeCount,
        total: totalCount,
        state,
        isToday: key === todayStr,
        isPast: key < todayStr,
      });
    }
    // pad to full weeks
    while (cells.length % 7 !== 0) {
      cells.push({
        date: null,
        day: null,
        inMonth: false,
        available: 0,
        total: 0,
        state: 'none',
        isToday: false,
        isPast: false,
      });
    }
    return cells;
  });

  protected readonly selectedDay = computed<AvailabilityDay | null>(() => {
    const key = this.selectedDate();
    return key ? (this.dayMap().get(key) ?? null) : null;
  });

  protected readonly canPrevMonth = computed(() => {
    const now = this.firstOfMonth(new Date());
    return this.viewMonth() > now;
  });

  ngOnDestroy(): void {
    this.clearEmailResendCooldownTimer();
    this.clearAddressSearchTimer();
    this.removeTurnstileWidget();
  }

  ngAfterViewInit(): void {
    this.turnstileViewReady = true;
    this.renderTurnstileIfNeeded();
  }

  ngOnInit(): void {
    // Only call the API in the browser - during SSR a relative '/api' URL has no host.
    if (!isPlatformBrowser(this.platformId)) return;

    this.api.getPublicConfig().subscribe({
      next: (config) => {
        this.turnstileEnabled.set(config.turnstile.enabled);
        this.turnstileSiteKey = config.turnstile.siteKey;
        this.renderTurnstileIfNeeded();
      },
      error: () => {
        this.turnstileEnabled.set(false);
      },
    });

    this.api.getAvailability().subscribe({
      next: (res) => {
        this.timeZone.set(res.timeZone);
        const map = new Map<string, AvailabilityDay>();
        for (const day of res.days) map.set(day.date, day);
        this.dayMap.set(map);

        // jump the view to the first month that has availability, and preselect first free day
        const firstFree = res.days.find((d) => d.slots.some((s) => s.available));
        if (firstFree) {
          this.viewMonth.set(this.firstOfMonth(new Date(firstFree.date + 'T00:00:00')));
          this.selectedDate.set(firstFree.date);
        }
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }

  prevMonth(): void {
    if (!this.canPrevMonth()) return;
    const d = new Date(this.viewMonth());
    d.setMonth(d.getMonth() - 1);
    this.viewMonth.set(d);
  }

  nextMonth(): void {
    const d = new Date(this.viewMonth());
    d.setMonth(d.getMonth() + 1);
    this.viewMonth.set(d);
  }

  selectCell(cell: CalCell): void {
    if (!cell.date || cell.available === 0) return;
    this.selectedDate.set(cell.date);
    this.selectedSlot.set(null);
  }

  selectSlot(slot: AvailabilitySlot): void {
    if (!slot.available) return;
    this.selectedSlot.set(slot);
  }

  setKind(k: MeetingKind): void {
    this.kind.set(k);
    this.submitError.set(null);

    if (k !== 'InPerson') {
      this.meetingLocation = '';
      this.addressSuggestions.set([]);
      this.addressSearchError.set(false);
      this.addressSuggestionChosen.set(false);
      this.clearAddressSearchTimer();
    }
  }

  onEmailChanged(): void {
    const normalized = this.normalizedEmail();
    if (this.verificationEmail() !== normalized) {
      this.emailVerificationId.set(null);
      this.emailVerificationToken.set(null);
      this.verifiedEmail.set(null);
      this.emailCode = '';
      this.emailVerificationInfo.set(null);
      this.stopEmailResendCooldown();
    }

    this.emailVerificationError.set(null);
  }

  emailIsValid(): boolean {
    const email = this.normalizedEmail();
    return /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(email);
  }

  emailIsVerified(): boolean {
    return !!this.emailVerificationToken() && this.verifiedEmail() === this.normalizedEmail();
  }

  canRequestEmailVerification(): boolean {
    return (
      this.emailIsValid() &&
      this.turnstileReadyForEmail() &&
      !this.emailVerificationBusy() &&
      !this.emailIsVerified() &&
      this.emailResendCooldownSeconds() === 0
    );
  }

  emailVerificationActionLabel(): string {
    if (this.emailVerificationBusy() && !this.emailVerificationId()) {
      return this.i18n.t('book.email.sending');
    }

    const remaining = this.emailResendCooldownSeconds();
    if (remaining > 0) {
      return this.i18n.t('book.email.resendInShort', { seconds: remaining });
    }

    return this.emailVerificationId()
      ? this.i18n.t('book.email.resendCode')
      : this.i18n.t('book.email.sendCode');
  }

  emailVerificationHint(): string | null {
    if (this.emailIsVerified()) {
      return null;
    }

    const remaining = this.emailResendCooldownSeconds();
    if (remaining > 0) {
      return this.i18n.t('book.email.resendIn', { seconds: remaining });
    }

    if (this.email.trim().length > 0 && !this.emailIsValid()) {
      return this.i18n.t('book.email.invalid');
    }

    if (this.emailIsValid() && !this.turnstileReadyForEmail()) {
      return this.i18n.t('book.turnstile.required');
    }

    if (this.emailVerificationId()) {
      return this.i18n.t('book.email.enterCodeHint');
    }

    return null;
  }

  canConfirmEmailVerification(): boolean {
    return (
      !!this.emailVerificationId() &&
      /^\d{6}$/.test(this.emailCode.trim()) &&
      this.emailIsValid() &&
      !this.emailVerificationBusy() &&
      !this.emailIsVerified()
    );
  }

  requestEmailVerification(): void {
    if (!this.emailIsValid()) {
      this.emailVerificationError.set(this.i18n.t('book.email.invalid'));
      return;
    }

    if (!this.turnstileReadyForEmail()) {
      this.emailVerificationError.set(this.i18n.t('book.turnstile.required'));
      return;
    }

    if (!this.canRequestEmailVerification()) {
      return;
    }

    this.emailVerificationBusy.set(true);
    this.emailVerificationError.set(null);
    this.emailVerificationInfo.set(null);
    this.emailVerificationToken.set(null);
    this.verifiedEmail.set(null);
    this.emailCode = '';

    const email = this.normalizedEmail();
    const turnstileToken = this.turnstileToken();
    this.api
      .requestEmailVerification({ email, language: this.i18n.lang(), turnstileToken })
      .subscribe({
        next: (res) => {
          this.emailVerificationId.set(res.verificationId);
          this.verificationEmail.set(email);
          this.emailVerificationInfo.set(this.i18n.t('book.email.codeSent'));
          this.startEmailResendCooldown();
          this.resetTurnstile();
          this.emailVerificationBusy.set(false);
        },
        error: (err) => {
          const code = this.apiErrorCode(err);
          if (code === 'email_code_recently_sent') {
            this.startEmailResendCooldown();
          }

          this.emailVerificationError.set(this.translateApiError(err, 'book.email.sendError'));
          this.resetTurnstile();
          this.emailVerificationBusy.set(false);
        },
      });
  }

  confirmEmailVerification(): void {
    const verificationId = this.emailVerificationId();
    if (!verificationId || !this.canConfirmEmailVerification()) return;

    this.emailVerificationBusy.set(true);
    this.emailVerificationError.set(null);

    const email = this.normalizedEmail();
    this.api
      .confirmEmailVerification({
        verificationId,
        email,
        code: this.emailCode.trim(),
      })
      .subscribe({
        next: (res) => {
          this.verifiedEmail.set(res.email);
          this.emailVerificationToken.set(res.emailVerificationToken);
          this.emailVerificationInfo.set(this.i18n.t('book.email.verified'));
          this.stopEmailResendCooldown();
          this.emailVerificationBusy.set(false);
        },
        error: (err) => {
          this.emailVerificationError.set(this.translateApiError(err, 'book.email.verifyError'));
          this.emailVerificationBusy.set(false);
        },
      });
  }

  canSubmit(): boolean {
    return (
      !!this.selectedSlot() &&
      this.name.trim().length > 1 &&
      this.emailIsValid() &&
      this.emailIsVerified() &&
      this.message.trim().length >= 10 &&
      this.meetingLocationIsValid() &&
      !this.submitting()
    );
  }

  submit(): void {
    const slot = this.selectedSlot();
    if (!slot || !this.canSubmit()) return;

    this.submitting.set(true);
    this.submitError.set(null);

    this.api
      .createBooking({
        slotId: slot.id,
        name: this.name.trim(),
        email: this.normalizedEmail(),
        message: this.message.trim(),
        kind: this.kind(),
        meetingLocation: this.kind() === 'InPerson' ? this.meetingLocation.trim() : null,
        emailVerificationToken: this.emailVerificationToken() ?? '',
        language: this.i18n.lang(),
      })
      .subscribe({
        next: (res) => {
          this.result.set(res);
          this.submitting.set(false);
        },
        error: (err) => {
          this.submitError.set(this.translateApiError(err, 'book.submit.error'));
          this.submitting.set(false);
        },
      });
  }

  copyManageLink(): void {
    const url = this.result()?.manageUrl;
    if (!url || typeof navigator === 'undefined' || !navigator.clipboard) return;
    navigator.clipboard.writeText(url).then(() => {
      this.copied.set(true);
      window.setTimeout(() => this.copied.set(false), 2000);
    });
  }

  reset(): void {
    this.result.set(null);
    this.selectedSlot.set(null);
    this.name = '';
    this.email = '';
    this.emailCode = '';
    this.message = '';
    this.meetingLocation = '';
    this.addressSuggestions.set([]);
    this.addressSearchBusy.set(false);
    this.addressSearchError.set(false);
    this.addressSuggestionChosen.set(false);
    this.clearAddressSearchTimer();
    this.emailVerificationId.set(null);
    this.verificationEmail.set(null);
    this.emailVerificationToken.set(null);
    this.verifiedEmail.set(null);
    this.emailVerificationError.set(null);
    this.emailVerificationInfo.set(null);
    this.stopEmailResendCooldown();
    this.resetTurnstile();
    this.api.getAvailability().subscribe({
      next: (res) => {
        const map = new Map<string, AvailabilityDay>();
        for (const day of res.days) map.set(day.date, day);
        this.dayMap.set(map);
      },
    });
  }

  meetingLocationIsValid(): boolean {
    return this.kind() !== 'InPerson' || this.meetingLocation.trim().length >= 5;
  }

  onMeetingLocationChanged(value: string): void {
    this.meetingLocation = value;
    this.addressSearchError.set(false);
    this.addressSuggestionChosen.set(false);

    const query = value.trim();
    if (query.length < 3 || !isPlatformBrowser(this.platformId)) {
      this.addressSuggestions.set([]);
      this.addressSearchBusy.set(false);
      this.clearAddressSearchTimer();
      return;
    }

    this.clearAddressSearchTimer();
    this.addressSearchBusy.set(true);

    this.addressSearchTimer = window.setTimeout(() => {
      const sequence = ++this.addressSearchSequence;
      this.api.searchBelgianAddresses(query).subscribe({
        next: (suggestions) => {
          if (sequence !== this.addressSearchSequence) return;

          this.addressSuggestions.set(suggestions);
          this.addressSearchBusy.set(false);
        },
        error: () => {
          if (sequence !== this.addressSearchSequence) return;

          this.addressSuggestions.set([]);
          this.addressSearchBusy.set(false);
          this.addressSearchError.set(true);
        },
      });
    }, 350);
  }

  chooseAddressSuggestion(suggestion: AddressSuggestion): void {
    this.meetingLocation = suggestion.label;
    this.addressSuggestions.set([]);
    this.addressSearchBusy.set(false);
    this.addressSearchError.set(false);
    this.addressSuggestionChosen.set(true);
    this.clearAddressSearchTimer();
  }

  addressSearchHint(): string | null {
    if (this.kind() !== 'InPerson') return null;

    if (this.addressSearchBusy()) {
      return this.i18n.t('book.location.searching');
    }

    if (this.addressSearchError()) {
      return this.i18n.t('book.location.searchError');
    }

    if (this.addressSuggestionChosen()) return null;

    if (this.meetingLocation.trim().length > 0 && this.meetingLocation.trim().length < 3) {
      return this.i18n.t('book.location.searchHint');
    }

    if (this.meetingLocation.trim().length >= 3 && this.addressSuggestions().length === 0) {
      return this.i18n.t('book.location.noResults');
    }

    return null;
  }

  private clearAddressSearchTimer(): void {
    if (this.addressSearchTimer === null) return;

    window.clearTimeout(this.addressSearchTimer);
    this.addressSearchTimer = null;
  }

  kindLabel(kind: MeetingKind): string {
    const item = this.meetingKinds.find((k) => k.value === kind);
    return item ? this.i18n.t(item.labelKey) : kind;
  }

  timeZoneLabel(): string {
    return this.timeZone() === 'Europe/Brussels'
      ? this.i18n.t('book.timezone.brussels')
      : this.timeZone();
  }

  private emailResendCooldownTimer: number | null = null;

  private startEmailResendCooldown(seconds = BookComponent.emailResendCooldownSeconds): void {
    this.emailResendCooldownSeconds.set(seconds);
    this.clearEmailResendCooldownTimer();

    if (!isPlatformBrowser(this.platformId)) return;

    this.emailResendCooldownTimer = window.setInterval(() => {
      const next = Math.max(0, this.emailResendCooldownSeconds() - 1);
      this.emailResendCooldownSeconds.set(next);

      if (next === 0) {
        this.clearEmailResendCooldownTimer();
      }
    }, 1000);
  }

  private stopEmailResendCooldown(): void {
    this.emailResendCooldownSeconds.set(0);
    this.clearEmailResendCooldownTimer();
  }

  private clearEmailResendCooldownTimer(): void {
    if (this.emailResendCooldownTimer === null) return;

    window.clearInterval(this.emailResendCooldownTimer);
    this.emailResendCooldownTimer = null;
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

  private normalizedEmail(): string {
    return this.email.trim().toLowerCase();
  }

  protected turnstileReadyForEmail(): boolean {
    return !this.turnstileEnabled() || !!this.turnstileToken();
  }

  protected turnstileStatusLabel(): string {
    if (this.turnstileToken()) {
      return this.i18n.t('book.turnstile.ready');
    }

    if (this.turnstileLoading()) {
      return this.i18n.t('book.turnstile.loading');
    }

    return this.i18n.t('book.turnstile.required');
  }

  private renderTurnstileIfNeeded(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    if (!this.turnstileViewReady || !this.turnstileEnabled() || !this.turnstileSiteKey) return;
    if (this.turnstileWidgetId || !this.turnstileContainer?.nativeElement) return;

    this.turnstileLoading.set(true);
    this.turnstileError.set(null);

    this.loadTurnstileScript()
      .then(() => {
        if (!window.turnstile || !this.turnstileContainer?.nativeElement || this.turnstileWidgetId)
          return;

        this.turnstileWidgetId = window.turnstile.render(this.turnstileContainer.nativeElement, {
          sitekey: this.turnstileSiteKey,
          theme: 'dark',
          size: 'flexible',
          callback: (token: string) => {
            this.turnstileToken.set(token);
            this.turnstileError.set(null);
            this.turnstileLoading.set(false);
          },
          'expired-callback': () => {
            this.turnstileToken.set(null);
            this.turnstileError.set(this.i18n.t('book.turnstile.expired'));
            this.turnstileLoading.set(false);
          },
          'error-callback': () => {
            this.turnstileToken.set(null);
            this.turnstileError.set(this.i18n.t('book.turnstile.error'));
            this.turnstileLoading.set(false);
          },
        });
      })
      .catch(() => {
        this.turnstileToken.set(null);
        this.turnstileLoading.set(false);
        this.turnstileError.set(this.i18n.t('book.turnstile.error'));
      });
  }

  private loadTurnstileScript(): Promise<void> {
    if (window.turnstile) {
      return Promise.resolve();
    }

    if (this.turnstileScriptPromise) {
      return this.turnstileScriptPromise;
    }

    this.turnstileScriptPromise = new Promise<void>((resolve, reject) => {
      const existing = document.querySelector<HTMLScriptElement>('script[data-turnstile="true"]');
      if (existing) {
        existing.addEventListener('load', () => resolve(), { once: true });
        existing.addEventListener('error', () => reject(new Error('Turnstile script failed.')), {
          once: true,
        });
        return;
      }

      const script = document.createElement('script');
      script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
      script.async = true;
      script.defer = true;
      script.dataset['turnstile'] = 'true';
      script.addEventListener('load', () => resolve(), { once: true });
      script.addEventListener('error', () => reject(new Error('Turnstile script failed.')), {
        once: true,
      });
      document.head.appendChild(script);
    }).then(() => {
      if (!window.turnstile) {
        throw new Error('Turnstile API unavailable.');
      }
    });

    return this.turnstileScriptPromise;
  }

  private resetTurnstile(): void {
    this.turnstileToken.set(null);
    if (!isPlatformBrowser(this.platformId) || !this.turnstileWidgetId || !window.turnstile) return;
    window.turnstile.reset(this.turnstileWidgetId);
  }

  private removeTurnstileWidget(): void {
    if (!isPlatformBrowser(this.platformId) || !this.turnstileWidgetId || !window.turnstile?.remove)
      return;
    window.turnstile.remove(this.turnstileWidgetId);
    this.turnstileWidgetId = null;
  }

  // formatting
  longDate(key: string): string {
    return this.formatDate(this.dateFromKey(key), {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
    });
  }

  slotTime(iso: string): string {
    return new Date(iso).toLocaleTimeString(this.locale(), { hour: '2-digit', minute: '2-digit' });
  }

  fullWhen(iso: string): string {
    return this.formatDate(new Date(iso), {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  private formatDate(
    date: Date,
    options: Intl.DateTimeFormatOptions,
    capitalizedParts: string[] = ['weekday', 'month'],
  ): string {
    return new Intl.DateTimeFormat(this.locale(), options)
      .formatToParts(date)
      .map((part) =>
        capitalizedParts.includes(part.type) ? this.capitalizeFirst(part.value) : part.value,
      )
      .join('');
  }

  private capitalizeFirst(value: string): string {
    return value.length === 0
      ? value
      : value.charAt(0).toLocaleUpperCase(this.locale()) + value.slice(1);
  }

  private dateFromKey(key: string): Date {
    const [year, month, day] = key.split('-').map(Number);
    return new Date(year, month - 1, day);
  }

  private locale(): string {
    switch (this.i18n.lang()) {
      case 'fr':
        return 'fr-BE';
      case 'nl':
        return 'nl-BE';
      default:
        return 'en-GB';
    }
  }

  private firstOfMonth(d: Date): Date {
    return new Date(d.getFullYear(), d.getMonth(), 1);
  }

  private toKey(d: Date): string {
    const pad = (n: number) => (n < 10 ? '0' : '') + n;
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  }
}
