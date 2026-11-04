import { pipelineSegments } from './overview';

describe('pipelineSegments', () => {
  it('groups statuses into stages and reports the offer-stage total from request_received', () => {
    const { segments, reachedOffer } = pipelineSegments([
      { status: 'request_received', count: 42 },
      { status: 'assigned', count: 10 },
      { status: 'in_progress', count: 8 },
      { status: 'certificate_submitted', count: 9 },
      { status: 'certificate_approved', count: 15 },
      { status: 'sent_to_client', count: 6 },
      { status: 'certificate_returned', count: 5 },
    ]);
    expect(reachedOffer).toBe(42);
    expect(segments.map((s) => [s.label, s.count])).toEqual([
      ['Offers', 42],
      ['In progress', 18],
      ['Review', 9],
      ['Approved', 21],
      ['Returned', 5],
    ]);
  });

  it('drops empty stages', () => {
    expect(pipelineSegments([]).segments).toEqual([]);
  });
});
