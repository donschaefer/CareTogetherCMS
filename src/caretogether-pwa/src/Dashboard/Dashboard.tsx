import { Box, Container, Grid, Stack, Typography } from '@mui/material';
import { useRecoilValueLoadable } from 'recoil';
import { locationConfigurationQuery, organizationConfigurationQuery } from '../Model/ConfigurationModel';
import useScreenTitle from '../Shell/ShellScreenTitle';
import { useDataLoaded } from '../Model/Data';
import { ProgressBackdrop } from '../Shell/ProgressBackdrop';
import FullCalendar from '@fullcalendar/react';
import dayGridPlugin from '@fullcalendar/daygrid';
import listPlugin from '@fullcalendar/list';
import { format } from 'date-fns';
import { EventInput, EventSourceInput } from '@fullcalendar/core/index.js';
import { partneringFamiliesData } from '../Model/ReferralsModel';
import { useLoadable } from '../Hooks/useLoadable';
import { ChildLocationPlan, CombinedFamilyInfo } from '../GeneratedClient';
import { personNameString } from '../Families/PersonName';
import { useFamilyLookup } from '../Model/DirectoryModel';
import { familyNameString } from '../Families/FamilyName';
import { useFilterMenu } from '../Generic/useFilterMenu';
import { FilterMenu } from '../Generic/FilterMenu';

// function renderEventContent(eventInfo: any) {
//   return (
//     <>
//       <b>{eventInfo.timeText}</b>
//       <i>{eventInfo.event.title}</i>
//     </>
//   )
// }

function familyPerson(family: CombinedFamilyInfo, personId: string) {
  const familyPeople = (family.family?.adults || []).map(adult => adult.item1!).concat(family.family?.children || []);
  return familyPeople.find(person => person.id === personId)!;
}

function Dashboard() {
  const organizationConfiguration = useRecoilValueLoadable(organizationConfigurationQuery);
  const locationConfiguration = useRecoilValueLoadable(locationConfigurationQuery);
  const partneringFamilies = useLoadable(partneringFamiliesData);

  const dataLoaded = useDataLoaded();

  const familyLookup = useFamilyLookup();

  useScreenTitle("Dashboard");

  const allArrangements = (partneringFamilies || []).flatMap(family =>
    (family.partneringFamilyInfo?.closedReferrals || []).concat(family.partneringFamilyInfo?.openReferral || []).flatMap(referral =>
      referral.arrangements || []).map(arrangement => ({ arrangement, person: familyPerson(family, arrangement.partneringFamilyPersonId!) })));

  const arrangementPlannedDurations = allArrangements.map(({ arrangement, person }) => ({
    title: `${personNameString(person)} - ${arrangement.arrangementType}`,
    start: arrangement.plannedStartUtc && format(arrangement.plannedStartUtc, "yyyy-MM-dd"),
    end: arrangement.plannedEndUtc && format(arrangement.plannedEndUtc, "yyyy-MM-dd"),
    backgroundColor: 'lightblue'
  } as EventInput))

  const arrangementActualStarts = allArrangements.filter(({ arrangement }) => arrangement.startedAtUtc).map(({ arrangement, person }) => ({
    title: `▶ ${personNameString(person)} - ${arrangement.arrangementType}`,
    date: arrangement.startedAtUtc
  } as EventInput));

  const arrangementActualEnds = allArrangements.filter(({ arrangement }) => arrangement.endedAtUtc).map(({ arrangement, person }) => ({
    title: `⏹ ${personNameString(person)} - ${arrangement.arrangementType}`,
    date: arrangement.endedAtUtc
  } as EventInput));

  const arrangementCompletedRequirements = allArrangements.flatMap(({ arrangement, person }) => arrangement.completedRequirements?.map(completed => ({
    title: `✅ ${personNameString(person)} - ${completed.requirementName}`,
    date: completed.completedAtUtc
  } as EventInput)));

  const allArrangementMissingRequirements = allArrangements.flatMap(({ arrangement, person }) =>
    (arrangement.missingRequirements || []).map(missing => ({ person, missing })));

  const arrangementPastDueRequirements = allArrangementMissingRequirements.filter(({ missing }) => missing.pastDueSince).map(({ missing, person }) => ({
    title: `❌ ${personNameString(person)} - ${missing.actionName}`,
    date: missing.pastDueSince && format(missing.pastDueSince, "yyyy-MM-dd"),
    color: 'red'
  } as EventInput));

  const arrangementUpcomingRequirements = allArrangementMissingRequirements.filter(({ missing }) => missing.dueBy).map(({ missing, person }) => ({
    title: `📅 ${personNameString(person)} - ${missing.actionName}`,
    date: missing.dueBy && format(missing.dueBy, "yyyy-MM-dd")
  } as EventInput));

  const arrangementActualChildcare = allArrangements.flatMap(({ arrangement, person }) => {
    const durationEntries = (arrangement.childLocationHistory || []).map((entry, index, history) => {
      const nextEntry = index < history.length - 1 ? history[index + 1] : null;
      const locationFamily = familyLookup(entry.childLocationFamilyId);
      return {
        title: `🤝🏻 ${personNameString(person)} - ${familyNameString(locationFamily)}`,
        start: entry.timestampUtc,
        backgroundColor: entry.plan === ChildLocationPlan.WithParent ? 'green' : '#a52a2a',
        end: nextEntry?.timestampUtc
      } as EventInput;
    });
    return durationEntries.filter(entry => entry.backgroundColor === '#a52a2a');
  });

  const arrangementPlannedChildcare = allArrangements.flatMap(({ arrangement, person }) => {
    const durationEntries = (arrangement.childLocationPlan || []).map((entry, index, plan) => {
      const nextEntry = index < plan.length - 1 ? plan[index + 1] : null;
      const locationFamily = familyLookup(entry.childLocationFamilyId);
      return {
        title: `✋🏻 ${personNameString(person)} - ${familyNameString(locationFamily)}`,
        start: entry.timestampUtc,
        backgroundColor: entry.plan === ChildLocationPlan.WithParent ? 'lightgreen' : '#e58a8a',
        end: nextEntry?.timestampUtc
      } as EventInput;
    });
    return durationEntries.filter(entry => entry.backgroundColor === '#e58a8a');
  });

  enum CalendarFilters {
    ArrangementPlannedDuration = "Arrangement - Planned Duration",
    ArrangementActualStartEndDates = "Arrangement - Actual Start & End Dates",
    ArrangementCompletedRequirements = "Arrangement - Completed Requirements",
    ArrangementPastDueRequirements = "Arrangement - Past-Due Requirements",
    ArrangementUpcomingRequirements = "Arrangement - Upcoming Requirements",
    ArrangementPlannedChildcare = "Arrangement - Planned Childcare",
    ArrangementActualChildcare = "Arrangement - Actual Childcare"
  }

  const { filterOptions, handleFilterChange } =
    useFilterMenu(Object.values(CalendarFilters), [
      CalendarFilters.ArrangementPlannedDuration,
      CalendarFilters.ArrangementActualStartEndDates,
      CalendarFilters.ArrangementCompletedRequirements,
      CalendarFilters.ArrangementPastDueRequirements,
      CalendarFilters.ArrangementUpcomingRequirements,
      CalendarFilters.ArrangementPlannedChildcare,
      CalendarFilters.ArrangementActualChildcare
    ]);

  function isSelected(option: string) {
    return filterOptions.find(filterOption => filterOption.key === option)?.selected || false;
  }

  const filteredEvents: EventSourceInput = ([] as EventInput[]).concat(
    isSelected(CalendarFilters.ArrangementPlannedDuration) ? arrangementPlannedDurations : [],
    isSelected(CalendarFilters.ArrangementActualStartEndDates) ? arrangementActualStarts.concat(arrangementActualEnds) : [],
    isSelected(CalendarFilters.ArrangementCompletedRequirements) ? arrangementCompletedRequirements : [],
    isSelected(CalendarFilters.ArrangementPastDueRequirements) ? arrangementPastDueRequirements : [],
    isSelected(CalendarFilters.ArrangementUpcomingRequirements) ? arrangementUpcomingRequirements : [],
    isSelected(CalendarFilters.ArrangementActualChildcare) ? arrangementActualChildcare : [],
    isSelected(CalendarFilters.ArrangementPlannedChildcare) ? arrangementPlannedChildcare : []);

  return ((!dataLoaded || locationConfiguration.state !== 'hasValue' && organizationConfiguration.state !== 'hasValue')
    ? <ProgressBackdrop>
      <p>Loading dashboard...</p>
    </ProgressBackdrop>
    : <Container maxWidth={false} sx={{ paddingLeft: '12px' }}>
      <Stack direction='column'>
        <Stack direction='row' justifyContent='space-between'>
          <Typography variant='h5' sx={{ marginTop: 3 }}>
            <strong>{locationConfiguration.contents?.name}</strong> ({organizationConfiguration.contents?.organizationName})
          </Typography>
          <Box sx={{ marginTop: 3 }}>
            <FilterMenu
              singularLabel={`Event`}
              pluralLabel={`Events`}
              filterOptions={filterOptions}
              handleFilterChange={handleFilterChange}
            />
          </Box>
        </Stack>
        <br />
        <Grid container>
          <Grid item xs={12}>
            <FullCalendar /* https://fullcalendar.io/docs/react */
              plugins={[dayGridPlugin, listPlugin]}
              initialView='dayGridMonth'
              headerToolbar={{
                left: 'prevYear,prev,today,next,nextYear',
                center: 'title',
                right: 'dayGridMonth,listWeek'
              }}
              weekends={true}
              //expandRows={true}
              events={filteredEvents}
              //eventContent={renderEventContent}
              eventClassNames={'wrap-event'}
            />
          </Grid>
        </Grid>
      </Stack>
    </Container>
  );
}

export { Dashboard };
