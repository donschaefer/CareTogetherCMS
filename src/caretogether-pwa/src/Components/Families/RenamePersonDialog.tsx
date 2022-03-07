import { useState } from 'react';
import { Grid, TextField } from '@mui/material';
import { Person } from '../../GeneratedClient';
import { useDirectoryModel } from '../../Model/DirectoryModel';
import { UpdateDialog } from '../UpdateDialog';

interface RenamePersonProps {
  familyId: string,
  person: Person,
  onClose: () => void
}

export function RenamePersonDialog({familyId, person, onClose}: RenamePersonProps) {
  const [fields, setFields] = useState({
    firstName: person.firstName as string,
    lastName: person.lastName as string
  });
  const {
    firstName, lastName } = fields;
  const directoryModel = useDirectoryModel();

  async function renamePerson() {
    if (firstName.length <= 0 || lastName.length <= 0) {
      alert("First and last name are required. Try again.");
    } else {
      await directoryModel.updatePersonName(familyId, person.id as string,
        firstName, lastName);
    }
  }

  return (
    <UpdateDialog title="Rename Person" onClose={onClose}
      onSave={renamePerson} enableSave={() => firstName!==person.firstName || lastName !== person.lastName}>
      <form noValidate autoComplete="off">
        <Grid container spacing={2}>
          <Grid item xs={12} sm={6}>
            <TextField required id="first-name" label="First Name" fullWidth size="small"
              value={firstName} onChange={e => setFields({...fields, firstName: e.target.value})} />
          </Grid>
          <Grid item xs={12} sm={6}>
            <TextField required id="last-name" label="Last Name" fullWidth size="small"
              value={lastName} onChange={e => setFields({...fields, lastName: e.target.value})} />
          </Grid>
        </Grid>
      </form>
    </UpdateDialog>
  );
}
